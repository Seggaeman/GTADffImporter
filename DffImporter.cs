/**********************************************************************
 *<
	FILE: DffImporter.cs

	DESCRIPTION:  GTA III/Vice City/San Andreas .dff importer

	AUTHOR: seggaeman

	CONTRIBUTORS: DexX (dff format information), REspawn (dff format information), Kam (GTA_Material.ms), gtamodding.com

 *>	Copyright (c) 2011, All Rights Reserved.
 **********************************************************************/

using System;
using System.Collections.Generic;
using Autodesk.Max;
using Autodesk.Max.Plugins;

namespace DffImporter
{
    enum gameVersion : uint { GTA_IIIA = 0x3ffff, GTA_IIIB = 0x800ffff, GTA_IIIC = 0x310, GTA_VCA = 0xc02ffff, GTA_VCB = 0x1003ffff, GTA_SA = 0x1803ffff };
    enum secIDs : uint { HANIM_PLG = 0x11e, FRAME = 0x253f2fe, GEOMETRY= 0x0f, GEOMETRY_LIST = 0x1a, EXTENSION = 0x03, MATERIAL= 0x07, MATERIAL_LIST = 0x08, 
                        TEXTURE= 0x06, STRUCT = 0x01, CLUMP = 0x10, MATERIAL_EFFECTS_PLG= 0x120, REFLECTION_MATERIAL= 0x253f2fc, SPECULAR_MATERIAL= 0x253f2f6 };
    enum custClassIDs : uint { GTAMAT_A = 0x48238272, GTAMAT_B = 0x48206285 };

    public class Hwnd32 : System.Windows.Forms.IWin32Window
    {
        private IntPtr m_hwnd;

        public Hwnd32(IntPtr handle)
        {
            m_hwnd = handle;
        }

        public IntPtr Handle
        {
            get { return m_hwnd; }
        }
    }

    /// <summary>
    /// This is the main class representing our plugin
    /// </summary>
    public partial class DffImporter : Autodesk.Max.Plugins.UtilityObj
    {
        /// <summary>
        /// Parameters used in this object
        /// </summary>


        public IGlobal global;
        Descriptor descriptor;
        private SelectorForm newForm;
        //private IInterface dffInterface;

        public DffImporter(IGlobal _global, Descriptor _descriptor)
        {
            this.global = _global;
            this.descriptor = _descriptor;
        }

        public override void BeginEditParams(IInterface ip, IIUtil iu)
        {
            //check file integrity
            newForm = new SelectorForm(ip, this.global);
            newForm.Activate();
            newForm.Show(new Hwnd32(ip.MAXHWnd));
            iu.CloseUtility();
        }

        public override void EndEditParams(IInterface ip, IIUtil iu)
        {

        }
     }
}
