using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.C1;

namespace Linnarsson.Dna
{
    /// <summary>
    /// Use to get access to the database. For historical reasons, make distinction between sample/cell metadata and expression database.
    /// </summary>
    public class DBFactory
    {
        public static IDB GetProjectDB()
        {
            if (Props.props.UseNewDbSetup)
                return new SampleChipDB();
            return new ProjectDB();
        }

        public static IExpressionDB GetExpressionDB()
        {
            if (Props.props.UseNewDbSetup)
                return new SampleChipDB();
            return new C1DB();
        }
    }
}
