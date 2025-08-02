using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace Mantis
{
    public class MantisInfo : GH_AssemblyInfo
    {
        public override string Name => "Mantis";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("0b08f44c-f7b1-4b72-be40-546c6f8c5ecf");

        //Return a string identifying you or your company.
        public override string AuthorName => "Muayyad Khatib";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "muayyadkhatib@gmail.com";

        //Return a string representing the version.  This returns the same version as the assembly.
        public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
    }
}