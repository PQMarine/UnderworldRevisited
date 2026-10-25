using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// settings.json as JsonUtility reads and writes it, without Unity - for the self-check only
    /// (the game itself uses JsonUtility). Reading: keys map to the table's entries by name,
    /// unknown keys are skipped, a missing key keeps UWUserData's initializer. Writing: every
    /// entry in the table's order, pretty printed with four spaces, no newline at the end, floats
    /// as the shortest form of their double without an exponent and with ".0" when whole, texts
    /// with \" and \\ escaped and other letters as they are, a missing text as "", an empty list
    /// as []. Control characters in a text are escaped as JSON has them; no fixture has one, so
    /// that part is not tied to JsonUtility.
    /// </summary>
    internal static class UWSettingsJson
    {
        public static UWUserData Read(string psJson)
        {
            int liAt = 0;
            object lOValue = fParseValue(psJson, ref liAt);

            if (!(lOValue is List<KeyValuePair<string, object>> lOObject))
                throw new FormatException("not a JSON object");

            UWUserData lOData = new UWUserData();

            foreach (KeyValuePair<string, object> lOPair in lOObject)
            {
                UWSettingDef lODef = UWUserSettingsTable.Find(lOPair.Key);

                if (lODef == null)
                    continue;

                switch (lODef)
                {
                    case UWBoolSettingDef _:
                        lODef.SetRaw(lOData, (bool)lOPair.Value);
                        break;

                    case UWIntSettingDef _:
                        lODef.SetRaw(lOData, int.Parse((string)((NumberToken)lOPair.Value).Text, CultureInfo.InvariantCulture));
                        break;

                    case UWFloatSettingDef _:
                        lODef.SetRaw(lOData, float.Parse(((NumberToken)lOPair.Value).Text, CultureInfo.InvariantCulture));
                        break;

                    case UWTextSettingDef _:
                        lODef.SetRaw(lOData, lOPair.Value as string ?? string.Empty);
                        break;

                    case UWListSettingDef _:
                        List<string> lsItems = new List<string>();

                        foreach (object lOItem in (List<object>)lOPair.Value)
                            lsItems.Add((string)lOItem);

                        lODef.SetRaw(lOData, lsItems);
                        break;
                }
            }

            return lOData;
        }

        public static string Write(UWUserData pOData)
        {
            StringBuilder lOText = new StringBuilder();

            lOText.Append("{\n");

            UWSettingDef[] lODefs = UWUserSettingsTable.All;

            for (int liAt = 0; liAt < lODefs.Length; liAt++)
            {
                lOText.Append("    \"").Append(lODefs[liAt].Key).Append("\": ");
                fWriteValue(lOText, lODefs[liAt].RawOf(pOData), lODefs[liAt]);

                if (liAt < lODefs.Length - 1)
                    lOText.Append(',');

                lOText.Append('\n');
            }

            lOText.Append('}');

            return lOText.ToString();
        }

        private static void fWriteValue(StringBuilder pOText, object pOValue, UWSettingDef pODef)
        {
            switch (pODef)
            {
                case UWBoolSettingDef _:
                    pOText.Append((bool)pOValue ? "true" : "false");
                    break;

                case UWIntSettingDef _:
                    pOText.Append(((int)pOValue).ToString(CultureInfo.InvariantCulture));
                    break;

                case UWFloatSettingDef _:
                    pOText.Append(FormatFloat((float)pOValue));
                    break;

                case UWTextSettingDef _:
                    fWriteString(pOText, (string)pOValue ?? string.Empty);
                    break;

                case UWListSettingDef _:
                    List<string> lsItems = (List<string>)pOValue;

                    if (lsItems == null || lsItems.Count == 0)
                    {
                        pOText.Append("[]");
                        break;
                    }

                    pOText.Append("[\n");

                    for (int liAt = 0; liAt < lsItems.Count; liAt++)
                    {
                        pOText.Append("        ");
                        fWriteString(pOText, lsItems[liAt] ?? string.Empty);

                        if (liAt < lsItems.Count - 1)
                            pOText.Append(',');

                        pOText.Append('\n');
                    }

                    pOText.Append("    ]");
                    break;
            }
        }

        /// <summary>A float as JsonUtility writes it: its double in the shortest form that reads
        /// back, never with an exponent (1E-05f is 0.000009999999747378752), ".0" when whole.</summary>
        public static string FormatFloat(float pfValue)
        {
            string lsText = ((double)pfValue).ToString("R", CultureInfo.InvariantCulture);
            int liExponent = lsText.IndexOfAny(new[] { 'E', 'e' });

            if (liExponent >= 0)
            {
                bool lbNegative = lsText.StartsWith("-", StringComparison.Ordinal);
                string lsMantissa = lsText.Substring(lbNegative ? 1 : 0, liExponent - (lbNegative ? 1 : 0));
                int liPower = int.Parse(lsText.Substring(liExponent + 1), CultureInfo.InvariantCulture);
                int liPoint = lsMantissa.IndexOf('.');
                string lsDigits = lsMantissa.Replace(".", string.Empty);
                int liPointAt = (liPoint < 0 ? lsMantissa.Length : liPoint) + liPower;

                if (liPointAt <= 0)
                    lsText = "0." + new string('0', -liPointAt) + lsDigits;
                else if (liPointAt >= lsDigits.Length)
                    lsText = lsDigits + new string('0', liPointAt - lsDigits.Length);
                else
                    lsText = lsDigits.Substring(0, liPointAt) + "." + lsDigits.Substring(liPointAt);

                if (lbNegative)
                    lsText = "-" + lsText;
            }

            if (lsText.IndexOf('.') < 0 && lsText.IndexOf('N') < 0 && lsText.IndexOf('I') < 0)
                lsText += ".0";

            return lsText;
        }

        private static void fWriteString(StringBuilder pOText, string psValue)
        {
            pOText.Append('"');

            foreach (char lcChar in psValue)
            {
                switch (lcChar)
                {
                    case '"': pOText.Append("\\\""); break;
                    case '\\': pOText.Append("\\\\"); break;
                    case '\n': pOText.Append("\\n"); break;
                    case '\r': pOText.Append("\\r"); break;
                    case '\t': pOText.Append("\\t"); break;
                    case '\b': pOText.Append("\\b"); break;
                    case '\f': pOText.Append("\\f"); break;

                    default:
                        if (lcChar < ' ')
                            pOText.Append("\\u").Append(((int)lcChar).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            pOText.Append(lcChar);

                        break;
                }
            }

            pOText.Append('"');
        }

        /// <summary>A number as it stood in the file, read by the field's type.</summary>
        private sealed class NumberToken
        {
            public string Text;
        }

        private static object fParseValue(string psJson, ref int piAt)
        {
            fSkipSpace(psJson, ref piAt);

            if (piAt >= psJson.Length)
                throw new FormatException("unexpected end");

            char lcChar = psJson[piAt];

            if (lcChar == '{')
            {
                List<KeyValuePair<string, object>> lOPairs = new List<KeyValuePair<string, object>>();

                piAt++;
                fSkipSpace(psJson, ref piAt);

                if (psJson[piAt] == '}')
                {
                    piAt++;

                    return lOPairs;
                }

                while (true)
                {
                    fSkipSpace(psJson, ref piAt);

                    string lsKey = fParseString(psJson, ref piAt);

                    fSkipSpace(psJson, ref piAt);
                    fExpect(psJson, ref piAt, ':');

                    object lOValue = fParseValue(psJson, ref piAt);

                    lOPairs.Add(new KeyValuePair<string, object>(lsKey, lOValue));
                    fSkipSpace(psJson, ref piAt);

                    if (psJson[piAt] == ',')
                    {
                        piAt++;
                        continue;
                    }

                    fExpect(psJson, ref piAt, '}');

                    return lOPairs;
                }
            }

            if (lcChar == '[')
            {
                List<object> lOItems = new List<object>();

                piAt++;
                fSkipSpace(psJson, ref piAt);

                if (psJson[piAt] == ']')
                {
                    piAt++;

                    return lOItems;
                }

                while (true)
                {
                    lOItems.Add(fParseValue(psJson, ref piAt));
                    fSkipSpace(psJson, ref piAt);

                    if (psJson[piAt] == ',')
                    {
                        piAt++;
                        continue;
                    }

                    fExpect(psJson, ref piAt, ']');

                    return lOItems;
                }
            }

            if (lcChar == '"')
                return fParseString(psJson, ref piAt);

            if (string.CompareOrdinal(psJson, piAt, "true", 0, 4) == 0)
            {
                piAt += 4;

                return true;
            }

            if (string.CompareOrdinal(psJson, piAt, "false", 0, 5) == 0)
            {
                piAt += 5;

                return false;
            }

            if (string.CompareOrdinal(psJson, piAt, "null", 0, 4) == 0)
            {
                piAt += 4;

                return null;
            }

            int liStart = piAt;

            while (piAt < psJson.Length && "+-0123456789.eE".IndexOf(psJson[piAt]) >= 0)
                piAt++;

            if (piAt == liStart)
                throw new FormatException("unexpected '" + lcChar + "' at " + piAt);

            return new NumberToken { Text = psJson.Substring(liStart, piAt - liStart) };
        }

        private static string fParseString(string psJson, ref int piAt)
        {
            fExpect(psJson, ref piAt, '"');

            StringBuilder lOText = new StringBuilder();

            while (psJson[piAt] != '"')
            {
                char lcChar = psJson[piAt++];

                if (lcChar != '\\')
                {
                    lOText.Append(lcChar);
                    continue;
                }

                char lcEscape = psJson[piAt++];

                switch (lcEscape)
                {
                    case 'n': lOText.Append('\n'); break;
                    case 'r': lOText.Append('\r'); break;
                    case 't': lOText.Append('\t'); break;
                    case 'b': lOText.Append('\b'); break;
                    case 'f': lOText.Append('\f'); break;

                    case 'u':
                        lOText.Append((char)int.Parse(psJson.Substring(piAt, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        piAt += 4;
                        break;

                    default:
                        lOText.Append(lcEscape);
                        break;
                }
            }

            piAt++;

            return lOText.ToString();
        }

        private static void fSkipSpace(string psJson, ref int piAt)
        {
            while (piAt < psJson.Length && char.IsWhiteSpace(psJson[piAt]))
                piAt++;
        }

        private static void fExpect(string psJson, ref int piAt, char pcWanted)
        {
            if (piAt >= psJson.Length || psJson[piAt] != pcWanted)
                throw new FormatException("expected '" + pcWanted + "' at " + piAt);

            piAt++;
        }
    }
}
