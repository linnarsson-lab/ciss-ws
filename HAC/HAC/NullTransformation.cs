using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    /// <summary>
    /// No transformation of data at all
    /// </summary>
    class NullTransformation : Transformation
    {
        public override void Transform(Element[] elements)
        {
            return;
        }
    }
}
