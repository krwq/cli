using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class ArgumentEscaper
    {
        /// <summary>
        /// Undo the processing which took place to create string[] args in Main,
        /// so that the next process will receive the same string[] args
        /// 
        /// See here for more info:
        /// http://blogs.msdn.com/b/twistylittlepassagesallalike/archive/2011/04/23/everyone-quotes-arguments-the-wrong-way.aspx
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string EscapeArgArray(string[] args)
        {
            return EscapeArgArray(args.AsEnumerable<string>());
        }

        /// <summary>
        /// Undo the processing which took place to create string[] args in Main,
        /// so that the next process will receive the same string[] args
        /// 
        /// See here for more info:
        /// http://blogs.msdn.com/b/twistylittlepassagesallalike/archive/2011/04/23/everyone-quotes-arguments-the-wrong-way.aspx
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string EscapeArgArray(IEnumerable<string> args)
        {
            var sb = new StringBuilder();
            var first = false;

            foreach (var arg in args)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    sb.Append(' ');
                }
                sb.Append(EscapeArg(arg));
            }

            return sb.ToString();
        }

        private static string EscapeArg(string arg)
        {
            var sb = new StringBuilder();

            // Always quote beginning and end to account for possible spaces
            sb.Append('"');

            for (int i=0; i < arg.Length; ++i)
            {
                var backslashCount = 0;

                // Consume All Backslashes
                while (i < arg.Length && arg[i] == '\\')
                {
                    backslashCount++;
                    i++;
                }

                // Escape any backslashes at the end of the arg
                // This ensures the outside quote is interpreted as
                // an argument delimiter
                if (i == arg.Length)
                {
                    sb.Append('\\', 2 * backslashCount);
                }

                // Escape any preceding backslashes and the quote
                else if(arg[i] == '"')
                {
                    sb.Append('\\', (2 * backslashCount) + 1);
                    sb.Append('"');
                }

                // Output any consumed backslashes and the character
                else
                {
                    sb.Append('\\', backslashCount);
                    sb.Append(arg[i]);
                }
            }

            sb.Append('"');

            return sb.ToString();
        }

        /// <summary>
        /// Prepare an already escaped and concatenated argument string to 
        /// roundtrip properly through cmd.
        /// 
        /// This prefixes every character with the '^' character to force cmd to
        /// interpret the argument string literally. An alternative option would 
        /// be to do this only for cmd metacharacters.
        /// 
        /// See here for more info:
        /// http://blogs.msdn.com/b/twistylittlepassagesallalike/archive/2011/04/23/everyone-quotes-arguments-the-wrong-way.aspx
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string EscapeArgStringForCmd(string arguments)
        {
            var sb = new StringBuilder();

            foreach (var character in arguments)
            {
                sb.Append('^');
                sb.Append(character);
            }

            return sb.ToString();
        }
    }
}
