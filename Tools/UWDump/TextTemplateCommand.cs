using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>The template of the port's own texts - needs no game data, so it skips the load.</summary>
    public static partial class Program
    {
        /// <summary>
        /// uwdump text-template &lt;code&gt; [--out file] [--from existing.tsv] (translation wave 6,
        /// 2026-10-26): the port's own texts (UWTextEnglish, about 870) with their ids and the English as
        /// a language file to translate into &lt;code&gt;.tsv - the same bytes as the editor menu
        /// Underworld Revisited / Text / Write the English texts (both UWTextTemplate.English). With
        /// --from a translation is brought up to date instead (UWTextTemplate.Update): its lines kept as
        /// written, the ids it lacks added as comments with their English ("# id TAB English" - remove
        /// the "# " and translate), the ids the table no longer knows listed on stderr and kept at the end
        /// as "# obsolete: ..." comments. Exit 0; 1 for a wrong call, 2 for a --from file that cannot be read.
        /// </summary>
        private static int fTextTemplate(DumpContext pOContext, List<string> pOArgs)
        {
            string lsOut = fTakeOption(pOArgs, "--out");
            string lsFrom = fTakeOption(pOArgs, "--from");

            if (pOArgs.Count < 1 || !fIsLanguageCode(pOArgs[0]))
            {
                Console.Error.WriteLine("Which language? A code of letters, digits, - and _, for example: uwdump text-template fr --out fr.tsv");

                return 1;
            }

            string lsCode = pOArgs[0];
            string lsText;
            string lsReport;

            if (lsFrom == null)
            {
                lsText = UWTextTemplate.English();
                lsReport = UWText.TextIds.Count + " texts in English to translate into " + lsCode;
            }
            else
            {
                string lsExisting;

                try
                {
                    lsExisting = File.ReadAllText(lsFrom, new UTF8Encoding(false, true));
                }
                catch (Exception lOError)
                {
                    Console.Error.WriteLine("Cannot read " + lsFrom + " as UTF-8: " + lOError.Message);

                    return 2;
                }

                UWTextTemplate.Result lOResult = UWTextTemplate.Update(lsExisting);

                lsText = lOResult.Text;
                lsReport = lsFrom + " brought up to date: " + lOResult.Kept + " lines kept, " + lOResult.Added.Count
                    + " new ids added as comments with their English, " + lOResult.Obsolete.Count + " obsolete";

                foreach (string lsId in lOResult.Added)
                    Console.Error.WriteLine("  new: " + lsId);

                foreach (string lsId in lOResult.Obsolete)
                    Console.Error.WriteLine("  obsolete: " + lsId);

                foreach (string lsProblem in lOResult.Problems)
                    Console.Error.WriteLine("  " + lsProblem);
            }

            byte[] lyText = new UTF8Encoding(false).GetBytes(lsText);

            if (lsOut != null)
            {
                File.WriteAllBytes(lsOut, lyText);
                lsReport += ", written to " + lsOut;
            }
            else
            {
                using (Stream lOStdout = Console.OpenStandardOutput())
                    lOStdout.Write(lyText, 0, lyText.Length);
            }

            Console.Error.WriteLine(lsReport);

            return 0;
        }
    }
}
