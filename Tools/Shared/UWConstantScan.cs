using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// EVERY CONSTANT of the engine-free layer by name and value: the const fields and the static
    /// readonly scalars and primitive arrays (the tables), and all of UWClassicLayout. The same
    /// scan as the self-check's constant pin (Pins/rule-constants.txt, PinChecks.fCheckConstants),
    /// shared here so `uwdump rules-template` prints exactly that list (cleanup 9.2,
    /// 2026-10-25); the pin check uses it too since cleanup 9.6 (the same day), so the two lists
    /// cannot drift apart.
    ///
    /// It is the inventory the rule set of the mod plan will be cut from (Docs/CLEANUP-PLAN.md
    /// section 6): until UWRules exists, the template is this list.
    /// </summary>
    internal static class UWConstantScan
    {
        /// <summary>Static readonly arrays that are state, not constants: the path search's scratch
        /// buffers and the creature motion's per-session quirk. They change while the game runs.</summary>
        private static readonly HashSet<string> msState = new HashSet<string>(StringComparer.Ordinal)
        {
            "UWTilePath.miStamp", "UWTilePath.miDistance", "UWTilePath.miQueue", "UWTilePath.miWaveCount",
            "UWCreatureMotion.msLavaFlagDropped",
        };

        /// <summary>Name to value, sorted ordinally; piTypesSkipped counts the types that could
        /// not be reflected.</summary>
        public static SortedDictionary<string, string> Scan(out int piTypesSkipped)
        {
            SortedDictionary<string, string> lONow = new SortedDictionary<string, string>(StringComparer.Ordinal);

            piTypesSkipped = 0;

            foreach (Type lOType in typeof(DataImport).Assembly.GetTypes())
            {
                FieldInfo[] lOFields;

                try
                {
                    lOFields = lOType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
                }
                catch
                {
                    piTypesSkipped++;

                    continue;
                }

                foreach (FieldInfo lOField in lOFields)
                {
                    string lsName = TypeName(lOType) + "." + lOField.Name;

                    if (msState.Contains(lsName))
                        continue;

                    try
                    {
                        if (lOField.IsLiteral)
                            lONow[lsName] = Format(lOField.GetRawConstantValue());
                        else if (lOField.IsInitOnly && (fIsPinnable(lOField.FieldType) || fIsLayoutTable(lOType)))
                            lONow[lsName] = Format(lOField.GetValue(null));
                    }
                    catch (Exception lOException)
                    {
                        lONow[lsName] = "<unreadable: " + lOException.GetType().Name + ">";
                    }
                }
            }

            return lONow;
        }

        public static string TypeName(Type pOType)
        {
            if (pOType.DeclaringType != null)
                return TypeName(pOType.DeclaringType) + "." + pOType.Name;

            return pOType.Name;
        }

        /// <summary>UWClassicLayout (cleanup 5.3, 2026-10-19) is the classic screen's pixel home: all
        /// of it is listed, its rectangles, colours and point tables too.</summary>
        private static bool fIsLayoutTable(Type pOType)
        {
            return pOType.DeclaringType == typeof(UWClassicLayout);
        }

        private static bool fIsPinnable(Type pOType)
        {
            if (pOType.IsArray)
            {
                Type lOElement = pOType.GetElementType();

                return lOElement != null && (lOElement.IsPrimitive || lOElement == typeof(string) || lOElement.IsEnum)
                    || (lOElement != null && lOElement.IsArray && fIsPinnable(lOElement));
            }

            return pOType.IsPrimitive || pOType == typeof(string) || pOType == typeof(decimal) || pOType.IsEnum;
        }

        public static string Format(object pOValue)
        {
            if (pOValue == null)
                return "null";

            if (pOValue is string lsText)
                return "\"" + lsText.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";

            if (pOValue is float lfValue)
                return lfValue.ToString("R", CultureInfo.InvariantCulture) + "f";

            if (pOValue is double ldValue)
                return ldValue.ToString("R", CultureInfo.InvariantCulture) + "d";

            if (pOValue is char lcValue)
                return "'" + lcValue + "'";

            if (pOValue is Array lOArray)
            {
                // Rows flattened; a two-dimensional table prints as one row (its shape is in the
                // length).
                StringBuilder lOText = new StringBuilder("[");
                bool lbFirst = true;

                foreach (object lOElement in lOArray)
                {
                    if (!lbFirst)
                        lOText.Append(", ");

                    lbFirst = false;
                    lOText.Append(Format(lOElement));
                }

                return lOText.Append("]").ToString();
            }

            if (pOValue is IFormattable lOFormattable)
                return lOFormattable.ToString(null, CultureInfo.InvariantCulture);

            // A struct of the layout table (UWRectInt, UWColor32): its public fields by name. A
            // value tuple prints itself.
            Type lOValueType = pOValue.GetType();

            if (lOValueType.IsValueType && !lOValueType.IsPrimitive && !lOValueType.IsEnum
                && !lOValueType.FullName.StartsWith("System.ValueTuple", StringComparison.Ordinal))
            {
                StringBuilder lOText = new StringBuilder("{");
                bool lbFirst = true;

                foreach (FieldInfo lOField in lOValueType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!lbFirst)
                        lOText.Append(", ");

                    lbFirst = false;
                    lOText.Append(lOField.Name).Append('=').Append(Format(lOField.GetValue(pOValue)));
                }

                return lOText.Append("}").ToString();
            }

            return pOValue.ToString();
        }
    }
}
