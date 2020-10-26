using System;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Max;
using Autodesk.Max.Plugins;

namespace DffImporter
{
	public partial class DffImporter : Autodesk.Max.Plugins.UtilityObj
	{
		/// <summary>
		/// Provides information about our plugin without having to create an instance of it
		/// </summary>
        /// 

		public class Descriptor : ClassDesc2
		{
			IGlobal global;
			internal static IClass_ID classID;

			public Descriptor( IGlobal global )
			{
				this.global = global;

				// The two numbers used for class id have to be unique/random
				classID = global.Class_ID.Create( 253674, 23564 );
			}

			public override string Category
			{
				get { return "none"; }
			}

			public override IClass_ID ClassID
			{
				get { return classID; }
			}

			public override string ClassName
			{
				get { return "GTA DFF Importer"; }
			}

			public override object Create( bool loading )
			{
				return new DffImporter( global, this );
			}

			public override bool IsPublic
			{
				// true to make our plugin visible in 3dsmax interface
				get { return true; }
			}

			public override SClass_ID SuperClassID
			{
                get { return SClass_ID.Utility; }
			}
		}

    }
}
