namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The short forms the older checks call; each hands on to Check.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fExpectBool(string psName, bool pbActual, bool pbExpected)
        {
            Check.Bool(psName, pbActual, pbExpected);
        }

        private static void fExpectInt(string psName, int piActual, int piExpected)
        {
            Check.Int(psName, piActual, piExpected);
        }

        private static void fPass(string psName, string psDetail)
        {
            Check.Pass(psName, psDetail);
        }

        private static void fFail(string psName, string psDetail)
        {
            Check.Fail(psName, psDetail);
        }
    }
}
