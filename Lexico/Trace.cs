using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Regex = System.Text.RegularExpressions.Regex;

namespace Lexico
{
    public struct StringSegment
    {
        public StringSegment(string str, int start, int length)
        {
            String = str;
            Start = start;
            Length = length;
        }

        public string String { get; }
        public int Start { get; }
        public int Length { get; }

        public override string ToString() => String?.Substring(Start, Length) ?? string.Empty;
    }

    /// <summary>
    /// Allows logging the results of each parser step for debugging, error reporting etc.
    /// </summary>
    public interface ITrace
    {
        /// <summary>
        /// Indicates that a child parser is being tested
        /// </summary>
        /// <param name="parser">The parser</param>
        /// <param name="name">The parser's name (relative to its parent)</param>
        void Push(IParser parser, string? name);

        /// <summary>
        /// Records the result of the matched Push record
        /// </summary>
        /// <param name="parser">The parser</param>
        /// <param name="success">True if matching succeeded</param>
        /// <param name="value">The resultant value</param>
        /// <param name="text">The total matched text</param>
        void Pop(IParser parser, bool success, object? value, StringSegment text);
    }

    public sealed class NoTrace : ITrace
    {
        public void Push(IParser parser, string? name) { }

        public void Pop(IParser parser, bool success, object? value, StringSegment text) { }
    }

    public abstract class DeveloperTrace : ITrace
    {
        int currentIndent = 0;
        private int IndentCount => Math.Max(0, SpacesPerIndent * currentIndent - (OutputBraces ? 0 : 2)); // -2 for leading outliner characters

        (IParser parser, string? name)? lastPush;
        readonly Stack<string> parserKindStack = new Stack<string>();

        protected abstract void WriteLine(bool isPop, bool success, string str);
        
        public bool OutputBraces { get; set; }
        public int SpacesPerIndent { get; set; } = 2;

        public void Pop(IParser parser, bool success, object? value, StringSegment text)
        {
            var sb = new StringBuilder();

            if (OutputBraces)
            {
                if (lastPush.HasValue)
                {
                    sb.Append(' ', IndentCount);
                    if (lastPush.Value.name != null)
                    {
                        sb.Append(lastPush.Value.name).Append(" : ");
                    }

                    sb.Append(lastPush.Value.parser?.ToString() ?? "<UNKNOWN>").Append(' ');
                    lastPush = null;
                }
                else
                {
                    currentIndent--;
                    sb.Append(' ', IndentCount).Append("} ");
                }

                if (success)
                {
                    sb.Append("\u2714").Append(" = ").Append(value ?? "<null>");
                }
                else
                {
                    if (text.String != null && text.Length == 0 && text.Start < text.String.Length)
                    {
                        text = new StringSegment(text.String, text.Start, text.Length + 1);
                    }

                    sb.Append("\u2717 (got `").Append(text).Append("`)");
                }
            }
            else
            {
                sb.Append(lastPush.HasValue ? "|" : "<")
                  .Append(success ? "\u2714 " : "\u2717 ");
                if (lastPush.HasValue)
                {
                    sb.Append(' ', IndentCount);
                    if (lastPush.Value.parser != null)
                    {
                        sb.Append(lastPush.Value.parser);
                    }

                    lastPush = null;
                }
                else
                {
                    currentIndent--;
                    sb.Append(' ', IndentCount).Append(parserKindStack.Pop());
                }

                sb.Append(success ? " \u2714 " : " \u2717 ");


                var result = text.ToString();

                void AppendResult(bool anyResult)
                {
                    if (anyResult)
                    {
                        sb.Append("`")
                          .Append(Regex.Replace(result, @"\r\n?|\n", @"\n"))
                          .Append("`");
                    }
                    else
                    {
                        sb.Append("<nothing>");
                    }
                }

                if (success) AppendResult(value != null);
                else AppendResult(text.Length > 0);
            }

            WriteLine(true, success, sb.ToString());
        }

        public void Push(IParser parser, string? name)
        {
            if (lastPush.HasValue) {
                WritePush(lastPush.Value.parser, lastPush.Value.name);
            }
            lastPush = (parser, name);
        }

        private void WritePush(IParser parser, string? name)
        {
            var sb = new StringBuilder();
            if (!OutputBraces) {
                sb.Append(">  ");
            }
            sb.Append(' ', IndentCount);

            switch (OutputBraces)
            {
                case true:
                    if (name != null) {
                        sb.Append(name).Append(" : ");
                    }
                    sb.Append(parser?.ToString() ?? "<UNKNOWN>").Append(" {");
                    break;
                case false:
                    var parserString = parser?.ToString() ?? "<UNKNOWN>";
                    parserKindStack.Push(parserString);
                    sb.Append(parserString);
                    break;
            }

            currentIndent++;
            WriteLine(false, false, sb.ToString());
        }
    }

    /// <summary>
    /// A Trace that writes directly and completely to the console (with colours)
    /// </summary>
    public sealed class ConsoleTrace : DeveloperTrace
    {
        protected override void WriteLine(bool isPop, bool success, string str)
        {
            Console.ForegroundColor = isPop ? success ? ConsoleColor.Green : ConsoleColor.Red : ConsoleColor.Yellow;
            Console.WriteLine(str);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }

    public sealed class DelegateDeveloperTrace : DeveloperTrace
    {
        public DelegateDeveloperTrace(Action<string> writeLine) => _writeLineDelegate = writeLine;

        private readonly Action<string> _writeLineDelegate;
        protected override void WriteLine(bool isPop, bool success, string str) => _writeLineDelegate?.Invoke(str);
    }

    public class UserTrace : ITrace
    {
        private readonly Action<string> _onError;

        public static Predicate<IParser> IgnoreRegexImpl => parser => parser is RegexImpl.Regex.Parser || parser is RegexImpl.SubstringParser || parser is RegexImpl.ConcatParser;
        public static Predicate<IParser> IgnoreOptionalWhitespace => parser => parser is OptionalParser op && op.Child is WhitespaceParser;

        public static IEnumerable<Predicate<IParser>> DefaultIgnoreRules => new[]
        {
            IgnoreRegexImpl,
            IgnoreOptionalWhitespace
        };

        protected UserTrace(Action<string> onError, List<Predicate<IParser>>? ignoreRules = null)
        {
            _onError = onError;
            _parserIgnoreRules = ignoreRules ?? DefaultIgnoreRules.ToList();
        }

        private TraceInstance? _trace;
        
        public void Push(IParser parser, string? name)
        {
            _trace ??= new TraceInstance(this);
            _trace.Push(parser, name);
        }

        public void Pop(IParser parser, bool success, object? value, StringSegment text)
        {
            if (_trace == null) throw new InvalidOperationException("Push was not called before Pop");
            _trace.Pop(parser, success, value, text);
            if (_trace.HasFinishedTrace) _trace = null;
        }

        public bool ShowAlternativeErrorBranches { get; set; } = true;
        public bool IgnoreBranchesThatConsumeNothing { get; set; } = true;
        public bool IgnoreRootParser { get; set; } = true;
        public bool ShowParserStack { get; set; } = true;

        private readonly List<Predicate<IParser>> _parserIgnoreRules;
        private class TraceInstance : ITrace
        {
            private class ErrorBranch
            {
                public ErrorBranch(Error rootError) => Errors = new List<Error> {rootError};

                public bool IgnoreFurtherErrors { get; set; }
                public IReadOnlyList<IParser> OuterParsers { get; set; } = Array.Empty<IParser>();
                public List<Error> Errors { get; }
                public Error InnermostError => Errors.First();
                public Error OutermostError => Errors.Last();
                public int TotalParsedCharacters => Errors.First().ErrorSegment.Start;
                public int SuccessfullyParsedCharacters => TotalParsedCharacters - Errors.Last().ErrorSegment.Start;
            }

            private class Error
            {
                public Error(IParser parser, StringSegment errorSegment)
                {
                    Parser = parser;
                    ErrorSegment = errorSegment;
                }

                public IParser Parser { get; }
                public StringSegment ErrorSegment { get; }
            }

            public TraceInstance(UserTrace userTrace) => _userTrace = userTrace;
            public bool HasFinishedTrace { get; private set; }

            private bool ParserShouldBeIgnored(IParser parser) => _userTrace._parserIgnoreRules.Any(rule => rule(parser));
            
            private readonly UserTrace _userTrace;
            private readonly List<ErrorBranch> _errorsToLog = new List<ErrorBranch>();
            private readonly Stack<IParser> _parserStack = new Stack<IParser>();
            private readonly Stack<int> _ignoreLevels = new Stack<int>();
            private ErrorBranch? _currentErrorBranch;
            private int _level;

            public void Push(IParser parser, string? name)
            {
                _level++;
                _parserStack.Push(parser);

                if (ParserShouldBeIgnored(parser))
                {
                    _ignoreLevels.Push(_level);
                }

                _currentErrorBranch = null;
            }

            public void Pop(IParser parser, bool success, object? value, StringSegment text)
            {
                _level--;
                _parserStack.Pop();

                if (_level <= 0) // We finished tracing
                {
                    FinalizeTrace();
                    return;
                }

                if (_ignoreLevels.Count > 0 && _ignoreLevels.Peek() >= _level)
                {
                    _ignoreLevels.Pop();
                }

                // We're not on an error branch if we're succeeding
                // Or if we're ignoring the current branch
                if (success || _ignoreLevels.Count > 0)
                {
                    if (_currentErrorBranch != null)
                    {
                        _currentErrorBranch.OuterParsers = _parserStack.ToList();
                        _currentErrorBranch = null;
                    }
                    return;
                }

                if (_currentErrorBranch == null)
                {
                    _currentErrorBranch = new ErrorBranch(new Error(parser, text));
                    _errorsToLog.Add(_currentErrorBranch);
                }
                else if (!_currentErrorBranch.IgnoreFurtherErrors)
                {
                    _currentErrorBranch.Errors.Add(new Error(parser, text));
                }
            }

            private void FinalizeTrace()
            {
                if (_errorsToLog.Count <= 0) return;
                var errorBranchesToLog = _errorsToLog
                                         .Take(_userTrace.IgnoreRootParser ? _errorsToLog.Count - 1 : _errorsToLog.Count) // Root parser is always the last failure so remove it here
                                         .OrderByDescending(branch => branch.TotalParsedCharacters)
                                         .Where(branch => !_userTrace.IgnoreBranchesThatConsumeNothing || branch.SuccessfullyParsedCharacters > 0)
                                         .GroupBy(branch => branch.TotalParsedCharacters)
                                         .ToArray();
                if (errorBranchesToLog.Length < 1) return; // Can happen if all branches are filtered out

                var builder = new StringBuilder();

                var indent = 0;
                void AppendLine(string? line = null)
                {
                    if (string.IsNullOrEmpty(line)) builder.AppendLine();
                    else builder.Append(' ', 4 * indent).AppendLine(line);
                }

                void AppendErrorWithCaret(ErrorBranch branch)
                {
                    var innermostError = branch.InnermostError;
                    var lines = innermostError.ErrorSegment.String.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
                    var errorIndex = innermostError.ErrorSegment.Start;
                    var errorLineNumber = innermostError.ErrorSegment.String.Take(errorIndex).Count(c => c == '\n');
                    var (errorChar, errorLineStartIndex) = innermostError.ErrorSegment.String.Select((ch, idx) => (ch, idx)).Take(errorIndex).Reverse().FirstOrDefault(tuple => tuple.ch == '\n');
                    var errorIndexOnLine = errorIndex - errorLineStartIndex;
                    indent++;

                    void AppendLineWithNumber(int number) => AppendLine($"{$"{number}:".PadLeft(4)} {lines[number]}");

                    AppendLine();
                    AppendLineWithNumber(errorLineNumber - 1);
                    AppendLineWithNumber(errorLineNumber);
                    AppendLine("^".PadLeft(errorIndexOnLine + 4));
                    if (errorLineNumber < lines.Length - 1) AppendLineWithNumber(errorLineNumber + 1); // Append line after if the error isn't the last line in the source
                    AppendLine();
                    indent--;
                }

                void AppendParserStack(bool showParserStack, ErrorBranch branch)
                {
                    if (!showParserStack) return;
                    AppendLine($"Parser Stack for: {branch.InnermostError.Parser}");
                    indent++;
                    foreach (var parser in branch.Errors.Select(err => err.Parser).Concat(branch.OuterParsers))
                    {
                        AppendLine(parser.ToString());
                    }
                    AppendLine();

                    indent--;
                }

                void AppendGroup(IEnumerable<ErrorBranch> errorBranches)
                {
                    var asArray = errorBranches.ToArray();
                    if (asArray.Length > 1)
                    {
                        AppendLine($"Parsing failed ambiguously - could not tell between {asArray.Length} parsers that all failed at this location.");
                        AppendLine($"Parsers attempted:    {string.Join("    ", asArray.Select(branch => branch.InnermostError.Parser))}");
                        AppendLine("Source text:");
                        AppendErrorWithCaret(asArray[0]); // When branches are grouped it's because they failed in the same place so we only append it once
                        indent++;
                        foreach (var errorBranch in asArray)
                        {
                            AppendParserStack(_userTrace.ShowParserStack, errorBranch);
                        }

                        indent--;
                    }
                    else
                    {
                        var errorBranch = asArray[0];
                        AppendLine($"Parsing failed - expected {errorBranch.InnermostError.Parser}");
                        AppendLine("Source text:");
                        AppendErrorWithCaret(errorBranch);
                        indent++;
                        AppendParserStack(_userTrace.ShowParserStack, errorBranch);
                        indent--;
                    }
                }

                AppendGroup(errorBranchesToLog[0]);

                if (_userTrace.ShowAlternativeErrorBranches && errorBranchesToLog.Length > 1)
                {
                    AppendLine();
                    AppendLine("=== Other parsers that were tried but failed sooner ===");
                    AppendLine();
                    indent++;
                    for (var groupIndex = 1; groupIndex < errorBranchesToLog.Length; groupIndex++)
                    {
                        AppendGroup(errorBranchesToLog[groupIndex]);
                    }

                    indent--;
                    AppendLine("=== End of parser alternatives ===");
                }

                _userTrace._onError(builder.ToString());
                HasFinishedTrace = true;
            }
        }
    }
}