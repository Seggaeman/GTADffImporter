using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.Max;
using Autodesk.Max.Plugins;

namespace DffImporter
{
    public partial class SelectorForm : Form
    {
        private System.IO.BinaryReader dffReader;
        /// <summary>
        /// object, light, camera, frame and geometry counts
        /// </summary>
        private System.UInt32[] OLCFG;
        private System.String fileName;
        private System.UInt32 version;
        private gtaFrame[] frameList;
        private ITriObject[] eMeshList;
        private IMultiMtl[] mtlList;
        private Autodesk.Max.IInterface ip;
        private Autodesk.Max.IGlobal global;
        private System.Byte[] data;
        private System.Int32 datIndex;
        private System.Collections.Generic.List<long> clumpAddr;
        private System.Windows.Forms.OpenFileDialog mDialog;

        struct gtaFrame
        {
            public IMatrix3 frameTM;
            public UInt32 parent;
            public System.String frameName;
            public UInt32 geoIndex;
            public IINode node;
            public ILightObject theLight;
        }

        public SelectorForm(Autodesk.Max.IInterface iPointer, Autodesk.Max.IGlobal _global)
        {
            this.ip = iPointer;
            this.global= _global;
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex != -1)
                readData(listBox1.SelectedIndex);
        }

        private void readData(System.Int32 index)
        {
            dffReader = new System.IO.BinaryReader(System.IO.File.Open(this.fileName, System.IO.FileMode.Open, System.IO.FileAccess.Read), Encoding.Default);
            dffReader.BaseStream.Seek((long)clumpAddr[index], System.IO.SeekOrigin.Begin);
            int length = (int)(clumpAddr[index + 1] - clumpAddr[index]);
            this.data = new System.Byte[length];
            dffReader.Read(this.data, 0, length);
            dffReader.Close();

            datIndex = 8;
            version = ReadUInt32();
            getFrames();
            getGeometry();
            getAtomic();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            mDialog.ShowDialog();
        }


        private void leHandler(System.Object sender, System.ComponentModel.CancelEventArgs argument)
        {
            listBox1.Items.Clear();
            this.fileName= mDialog.FileName;
            this.dffReader = new System.IO.BinaryReader(System.IO.File.Open(this.mDialog.FileName, System.IO.FileMode.Open, System.IO.FileAccess.Read), System.Text.Encoding.Default);
            this.clumpAddr = new System.Collections.Generic.List<long>();
            while (dffReader.BaseStream.Position < dffReader.BaseStream.Length)
            {
                if (dffReader.ReadUInt32() == (uint)secIDs.CLUMP)
                {
                    listBox1.Items.Add(dffReader.BaseStream.Position - 4);
                    clumpAddr.Add(dffReader.BaseStream.Position - 4);
                    dffReader.BaseStream.Seek(dffReader.ReadUInt32() + 4, System.IO.SeekOrigin.Current);
                }
                else
                    break;
            }
            clumpAddr.Add(dffReader.BaseStream.Length);
            dffReader.Close();
        }

        private void getFrames()
        {
            //get object, light and camera count
            datIndex += 12;
            OLCFG = new System.UInt32[5];
            if (version == (uint)gameVersion.GTA_IIIA || version == (uint)gameVersion.GTA_IIIB || version == (uint)gameVersion.GTA_IIIC)
            {
                OLCFG[0] = ReadUInt32();
                OLCFG[1] = 0; OLCFG[2] = 0;
            }
            else
                for (int i = 0; i < 3; ++i)
                    OLCFG[i] = ReadUInt32();

            //get number of frames
            datIndex += 24;
            OLCFG[3] = ReadUInt32();
            frameList = new gtaFrame[OLCFG[3]];

            
            //retrieve frame data (except names)
            for (int i = 0; i < OLCFG[3]; ++i)
            {
                frameList[i].frameTM = global.Matrix3.Create();
                for (int j = 0; j < 4; ++j)
                {
                    IPoint3 myPoint = global.Point3.Create(ReadSingle(), ReadSingle(), ReadSingle());
                    frameList[i].frameTM.SetRow(j, myPoint);
                }

                //get parent and set junk data
                frameList[i].parent = ReadUInt32();
                frameList[i].geoIndex = 0xffffffff;  //this value indicates a dummy node

                //skip this integer data
                datIndex += 4;

                //IDummyObject leDummy = (IDummyObject)ip.CreateInstance(SClass_ID.Helper, global.Class_ID.Create((uint)BuiltInClassIDA.DUMMY_CLASS_ID, 0));
                //frameList[i].node = ip.CreateObjectNode(leDummy);

                //frameList[i].node.SetNodeTM(0, frameList[i].frameTM);
                //frameList[i].node.Scale(0, frameList[i].frameTM, global.Point3.Create(10, 10, 10), true, false, (int)PivotMode.ObjectOnly, true);
            }
            
            //get frame names
            for (int i= 0; i < OLCFG[3]; ++i)
            {
                datIndex += 4;
                int secSize = ReadInt32();
                int refPos= datIndex;
                datIndex += 4;

                //check section type
                switch (ReadUInt32())
                {
                    case (uint)secIDs.HANIM_PLG:
                    {
                        int temp = ReadInt32();
                        datIndex += temp+4;
                        if (ReadUInt32() == (uint)secIDs.FRAME)
                        {
                            int size = ReadInt32();

                            datIndex += 4;
                            frameList[i].frameName = ReadString(ref size);
                        }
                    }
                    break;

                    case (uint)secIDs.FRAME:
                    {
                        int size = ReadInt32();
                        datIndex += 4;
                        frameList[i].frameName = ReadString(ref size);
                    }
                    break;

                    default:
                        frameList[i].frameName= System.String.Empty;
                    break;
                }
                //frameList[i].node.Name= frameList[i].frameName;
                //if (frameList[i].parent < OLCFG[3])
                    //frameList[frameList[i].parent].node.AttachChild(frameList[i].node, false);

                datIndex= refPos + secSize + 4;

            }
            
        }

        private void getGeometry()
        {
            //check for presence of geometry section
            if (ReadUInt32() != (uint)secIDs.GEOMETRY_LIST)
            {
                datIndex -= 4;
                return;
            }
            //geometry count
            datIndex += 20;
            OLCFG[4] = ReadUInt32();
            eMeshList = new ITriObject[OLCFG[4]];
            mtlList = new IMultiMtl[OLCFG[4]];

            //read the geometry data
            for (int i = 0; i < OLCFG[4]; ++i)
            {
                //skip geometry section and struct headers
                datIndex += 24;
                System.Byte flags = data[datIndex];
                //skip 1 useless byte (also adding +1 for flags)
                datIndex += 2;
                eMeshList[i] = (ITriObject)ip.CreateInstance(SClass_ID.Geomobject, global.Class_ID.Create((uint)BuiltInClassIDA.EDITTRIOBJ_CLASS_ID, 0));
                System.Byte numUVs = data[datIndex];

                //+1 for numUVs and +1 more for useless stuff
                datIndex += 2;
                eMeshList[i].Mesh.SetNumFaces(ReadInt32(), false, true);
                eMeshList[i].Mesh.SetNumVerts(ReadInt32(), false, true);
                datIndex += 4;

                //skip 12 bytes of colour data if file is a GTA III or GTA VC type A dff
                if (version == (uint)gameVersion.GTA_IIIA || version == (uint)gameVersion.GTA_IIIB || version == (uint)gameVersion.GTA_IIIC || version == (uint)gameVersion.GTA_VCA)
                    datIndex += 12;

                //check for vertex colors (RGBA)
                if ((flags & 8) == 8)
                {
                    eMeshList[i].Mesh.SetMapSupport(-2, true);
                    eMeshList[i].Mesh.SetMapSupport(0, true);
                    eMeshList[i].Mesh.SetNumMapVerts(-2, eMeshList[i].Mesh.NumVerts, false);
                    eMeshList[i].Mesh.SetNumMapFaces(-2, eMeshList[i].Mesh.NumFaces, false, 0);
                    eMeshList[i].Mesh.SetNumMapVerts(0, eMeshList[i].Mesh.NumVerts, false);
                    eMeshList[i].Mesh.SetNumMapFaces(0, eMeshList[i].Mesh.NumFaces, false, 0);

                    for (int ind = 0; ind < eMeshList[i].Mesh.NumVerts; ++ind)
                    {
                        //set vertex colors (RGB)
                        eMeshList[i].Mesh.MapVerts(0)[ind].X = data[datIndex] / 255.0f; datIndex++;
                        eMeshList[i].Mesh.MapVerts(0)[ind].Y = data[datIndex] / 255.0f; datIndex++;
                        eMeshList[i].Mesh.MapVerts(0)[ind].Z = data[datIndex] / 255.0f; datIndex++;

                        //vertex alpha, only x coordinate used
                        eMeshList[i].Mesh.MapVerts(-2)[ind].X = data[datIndex] / 255.0f; datIndex++;
                    }
                }

                //read UV verts
                for (short ind1 = 1; ind1 <= numUVs; ++ind1)
                {
                    eMeshList[i].Mesh.SetMapSupport(ind1, true);
                    eMeshList[i].Mesh.SetNumMapVerts(ind1, eMeshList[i].Mesh.NumVerts, false);
                    eMeshList[i].Mesh.SetNumMapFaces(ind1, eMeshList[i].Mesh.NumFaces, false, 0);
                    for (int ind2 = 0; ind2 < eMeshList[i].Mesh.NumVerts; ++ind2)
                    {
                        eMeshList[i].Mesh.MapVerts(ind1)[ind2].X = ReadSingle();
                        eMeshList[i].Mesh.MapVerts(ind1)[ind2].Y = 1.0f - ReadSingle();
                        eMeshList[i].Mesh.MapVerts(ind1)[ind2].Z = 0.0f;
                    }
                }

                //read faces, remember to build alpha, color and UV faces as well
                for (int ind = 0; ind < eMeshList[i].Mesh.NumFaces; ++ind)
                {
                    IPoint3 faceIndices = global.Point3.Create();
                    faceIndices.Y = ReadUInt16();
                    faceIndices.X = ReadUInt16();
                    // read material index (F in BAFC is the material index)
                    eMeshList[i].Mesh.Faces[ind].MatID = ReadUInt16();
                    faceIndices.Z = ReadUInt16();
                    eMeshList[i].Mesh.Faces[ind].SetVerts((int)faceIndices.X, (int)faceIndices.Y, (int)faceIndices.Z);
                    eMeshList[i].Mesh.Faces[ind].SetEdgeVisFlags(EdgeVisibility.Vis, EdgeVisibility.Vis, EdgeVisibility.Vis);
                    eMeshList[i].Mesh.Faces[ind].SmGroup = 1;
                    if ((flags & 8) == 8)
                    {
                        eMeshList[i].Mesh.MapFaces(-2)[ind].SetTVerts((int)faceIndices.X, (int)faceIndices.Y, (int)faceIndices.Z);
                        eMeshList[i].Mesh.MapFaces(0)[ind].SetTVerts((int)faceIndices.X, (int)faceIndices.Y, (int)faceIndices.Z);
                    }

                    //UV faces
                    for (int ind1 = 1; ind1 <= numUVs; ++ind1)
                        eMeshList[i].Mesh.MapFaces(ind1)[ind].SetTVerts((int)faceIndices.X, (int)faceIndices.Y, (int)faceIndices.Z);
                }
                //skip bounding information
                datIndex += 24;

                //check for vertex translation info
                //if ( (flags & 2) == 2) // this flag is sometimes 0 in VC and GTAIII even if translation info is present
                {
                    for (int ind = 0; ind < eMeshList[i].Mesh.NumVerts; ++ind)
                        eMeshList[i].Mesh.Verts[ind] = global.Point3.Create(ReadSingle(), ReadSingle(), ReadSingle());
                }

                eMeshList[i].Mesh.InvalidateEdgeList();

                //read normals
                if ((flags & 16) == 16)
                {
                    eMeshList[i].Mesh.SpecifyNormals();
                    IMeshNormalSpec nSpec = eMeshList[i].Mesh.SpecifiedNormals;
                    nSpec.BuildNormals();
                    nSpec.SetFlag(0x20, true);
                    nSpec.MakeNormalsExplicit(false, null, true);
                    
                    for (int ind = 0; ind < eMeshList[i].Mesh.NumFaces; ++ind)
                    {
                        for (int ind1 = 0; ind1 < 3; ++ind1)
                        {
                            int vIndex = (int)eMeshList[i].Mesh.Faces[ind].GetVert(ind1);
                            int normID = nSpec.Face(ind).GetNormalID(ind1);
                            nSpec.Normal(normID).X = BitConverter.ToSingle(data, datIndex + vIndex * 12);
                            nSpec.Normal(normID).Y = BitConverter.ToSingle(data, datIndex + vIndex * 12 + 4);
                            nSpec.Normal(normID).Z = BitConverter.ToSingle(data, datIndex + vIndex * 12 + 8);
                        }
                    }
                    
                    datIndex += eMeshList[i].Mesh.NumVerts * 3 * 4;
                }
                eMeshList[i].Mesh.BuildStripsAndEdges();
                getMaterials(ref i);
/*
                //skip material list
                if (ReadUInt32() == (uint)secIDs.MATERIAL_LIST)
                {
                    int temp = ReadInt32();
                    datIndex += temp + 4;
                }
                else
                    datIndex -= 4;

                //skip extension
                if (ReadUInt32() == (uint)secIDs.EXTENSION)
                {
                    int temp = ReadInt32();
                    datIndex += temp+ 4;
                }
                else
                    datIndex -= 4;
 */
            }
        }

        //number of atomic objects assumed equal to object count
        private void getAtomic()
        {
            for (int i=0; i < OLCFG[0]; ++i)
            {
                //skip atomic and child struct headers (24 bytes)
                datIndex += 24;
                int fIndex= ReadInt32();
                //set associated geometry index
                frameList[fIndex].geoIndex= ReadUInt32();
                //skip two unknowns
                datIndex += 8;
                //skip extension
                if (ReadUInt32() == (uint)secIDs.EXTENSION)
                {
                    int temp = ReadInt32();
                    datIndex += temp + 4;
                }
                else
                    datIndex -= 4;
            }

            //read omnilight data
            for (int ind = 0; ind < OLCFG[1]; ++ind)
            {
                //get index of light object from struct section
                datIndex += 12;
                int temp = ReadInt32();
                frameList[temp].geoIndex = 0xfffffffe;       //light
                frameList[temp].theLight = (ILightObject)ip.CreateInstance(SClass_ID.Light, global.Class_ID.Create((uint)BuiltInClassIDA.OMNI_LIGHT_CLASS_ID, 0));
                datIndex += 24;  //skip light section header and constituent struct
                frameList[temp].theLight.SetAtten(0, 0, ReadSingle());
                frameList[temp].theLight.SetRGBColor(0, global.Point3.Create(255.0*ReadSingle(), 255.0*ReadSingle(), 255.0*ReadSingle()));
                frameList[temp].theLight.SetFallsize(0, ReadSingle());
                datIndex += 8;      //skip light section header
                temp = ReadInt32();
                datIndex += temp + 4;
            }

            //read camera data       //not yet tested
            for (int ind = 0; ind < OLCFG[2]; ++ind)
            {
                //get index of camera object from struct section
                datIndex += 12;
                frameList[ReadInt32()].geoIndex = 0xfffffffd;       //camera
                datIndex += 4;  //skip light section identifier
                int temp = ReadInt32();
                datIndex += temp + 4;
            }

            //create objects and establish hierarchy
            for (int i= 0; i < OLCFG[3]; ++i)
            {
                switch (frameList[i].geoIndex)
                {
                    case 0xffffffff:
                    {
                        IDummyObject leDummy= (IDummyObject)ip.CreateInstance(SClass_ID.Helper, global.Class_ID.Create((uint)BuiltInClassIDA.DUMMY_CLASS_ID, 0));
                        frameList[i].node= ip.CreateObjectNode(leDummy);
                        frameList[i].node.Scale(0, frameList[i].frameTM, global.Point3.Create(10,10,10), true, false, (int)PivotMode.ObjectOnly, true);
                    }
                    break;
                    case 0xfffffffe:
                    {
                        frameList[i].node= ip.CreateObjectNode(frameList[i].theLight);
                    }
                    break;

                    case 0xfffffffd:
                    break;

                    default:
                    {
                        frameList[i].node= ip.CreateObjectNode(eMeshList[frameList[i].geoIndex]);
                        frameList[i].node.Mtl = mtlList[frameList[i].geoIndex];
                    }
                    break;
                }
                frameList[i].node.SetNodeTM(0, frameList[i].frameTM);
                frameList[i].node.Name= frameList[i].frameName;
            }
            //establish hierarchy
            for (int i= 0; i < OLCFG[3]; ++i)
                if(frameList[i].parent < OLCFG[3])
                    frameList[frameList[i].parent].node.AttachChild(frameList[i].node, false);
        }

        private void getMaterials(ref int geoIndex)
        {
            if (ReadUInt32() != (uint)secIDs.MATERIAL_LIST)
            {
                datIndex -= 4;
                return;
            }

            //get number of materials
            datIndex += 20;
            int matCount = ReadInt32();
            //skip trash FFFF data (one for each material)
            datIndex += matCount * 4;
            mtlList[geoIndex] = (IMultiMtl)ip.CreateInstance(SClass_ID.Material, global.Class_ID.Create((uint)BuiltInClassIDA.MULTI_CLASS_ID, 0));
            mtlList[geoIndex].SetNumSubMtls(matCount);

            //read material data
            for (int i = 0; i < matCount; ++i)
            {
                IMtl leMat = (IMtl)ip.CreateInstance(SClass_ID.Material, global.Class_ID.Create((uint)custClassIDs.GTAMAT_A, (uint)custClassIDs.GTAMAT_B));
                //skip material header, struct header and integer
                datIndex += 28;
                //read color
                leMat.GetParamBlock(0).SetValue(3, 0, global.Color.Create(data[datIndex] / 255.0f, data[datIndex + 1] / 255.0f, data[datIndex + 2] / 255.0f), 0);
                leMat.GetParamBlock(0).SetValue(6, 0, (int)data[datIndex + 3], 0);
                //increment and skip unknown
                datIndex += 8;
                uint numTextures = ReadUInt32();
                //ambient, diffuse and specular?
                leMat.GetParamBlock(0).SetValue(0, 0, ReadSingle(), 0);
                leMat.GetParamBlock(0).SetValue(1, 0, ReadSingle(), 0);
                leMat.GetParamBlock(0).SetValue(2, 0, ReadSingle(), 0);
                mtlList[geoIndex].SetSubMtl(i, leMat);
                //read textures
                for (int ind = 0; ind < numTextures; ++ind)
                {
                    //skip texture and struct header
                    datIndex += 24;
                    int filtAddr = datIndex;
                    datIndex += 4;      //skip three bytes (used later)+1 unknown.

                    //read diffuse texture name
                    datIndex += 4;      //skip header
                    int size = ReadInt32();
                    datIndex += 4;      //skip RW version
                    IBitmapTex diffTex = (IBitmapTex)ip.CreateInstance(SClass_ID.Texmap, global.Class_ID.Create((uint)BuiltInClassIDA.BMTEX_CLASS_ID, 0));
                    diffTex.Name = ReadString(ref size);

                    //read alpha texture name
                    datIndex += 4;      //skip header
                    size= ReadInt32();
                    datIndex += 4;      //skip RW version
                    IBitmapTex alphaTex= (IBitmapTex)ip.CreateInstance(SClass_ID.Texmap, global.Class_ID.Create((uint)BuiltInClassIDA.BMTEX_CLASS_ID, 0));
                    alphaTex.Name= ReadString(ref size);
                    
                    //set tiling and mirror data from filter flags
                    switch (data[filtAddr])
                    {
                        case 1:
                            diffTex.FilterType = 2;   //FILTER_NADA
                            alphaTex.FilterType = 2;
                            break;

                        case 2:
                            diffTex.FilterType = 1;  //FILTER_SAT    
                            alphaTex.FilterType = 1; //FILTER_SAT
                            break;

                        default:
                            diffTex.FilterType = 0;  //FILTER_PYR
                            alphaTex.FilterType = 0; //FILTER_PYR
                            break;
                    }

                    diffTex.UVGen.SetFlag(1 << 2, ~(data[filtAddr + 1] | 0xfffffffe));
                    diffTex.UVGen.SetFlag(1 << 0, ~(data[filtAddr + 1] | 0xfffffffd));
                    diffTex.UVGen.SetFlag(1 << 3, ~(data[filtAddr + 1] | 0xffffffef));
                    diffTex.UVGen.SetFlag(1 << 1, ~(data[filtAddr + 1] | 0xffffffdf));

                    alphaTex.UVGen.SetFlag(1 << 2, ~(data[filtAddr + 1] | 0xfffffffe));
                    alphaTex.UVGen.SetFlag(1 << 0, ~(data[filtAddr + 1] | 0xfffffffd));
                    alphaTex.UVGen.SetFlag(1 << 3, ~(data[filtAddr + 1] | 0xffffffef));
                    alphaTex.UVGen.SetFlag(1 << 1, ~(data[filtAddr + 1] | 0xffffffdf));

                    leMat.GetParamBlock(0).SetValue(4, 0, diffTex, 0);
                    leMat.GetParamBlock(0).SetValue(7, 0, alphaTex, 0);
                    leMat.GetParamBlock(0).SetValue(8, 0, 0, 0);

                    //skip extension
                    if (ReadUInt32() == (uint)secIDs.EXTENSION)
                    {
                        int temp = ReadInt32();
                        datIndex += temp + 4;
                    }
                    else
                    {
                        datIndex -= 4;
                    }
                }
                //read material effects
                if (ReadUInt32() == (uint)secIDs.EXTENSION)
                {
                    datIndex += 8;      //skip section size and RW version
                    //check for material effects, reflection and specular material
                    for (int ind=0; ind<3; ++ind)
                    {
                        switch(ReadUInt32())
                        {
                            case (uint) secIDs.MATERIAL_EFFECTS_PLG:
                            {
                                datIndex+=16; //skip section size, RW version and starting ints (both 2)
                                leMat.GetParamBlock(0).SetValue(9, 0, 100.0f*ReadSingle(), 0);      //reflection
                                leMat.GetParamBlock(0).SetValue(11, 0, 1, 0);
                                datIndex += 8;  //skip unknown (= 0), skip texture ON/OFF switch 0/1
                                if (ReadUInt32() == (uint)secIDs.TEXTURE)
                                {
                                    //skip texture and struct header
                                    datIndex += 20;
                                    int filtAddr= datIndex;
                                    datIndex += 4;  //skip 3 filtering bytes + unkown

                                    //read diffuse texture name
                                    datIndex += 4;      //skip header
                                    int size= ReadInt32();
                                    datIndex += 4;      //skip RW version
                                    IBitmapTex diffTex = (IBitmapTex)ip.CreateInstance(SClass_ID.Texmap, global.Class_ID.Create((uint)BuiltInClassIDA.BMTEX_CLASS_ID, 0));
                                    diffTex.Name = ReadString(ref size);

                                    //skip alpha texture
                                    if (ReadUInt32() == 0x02)
                                    {
                                        int temp= ReadInt32();
                                        datIndex += temp+4;
                                    }
                                    else
                                        datIndex -= 4;

                                    //set tiling and mirror data from filter flags
                                    switch (data[filtAddr])
                                    {
                                        case 1:
                                            diffTex.FilterType = 2;   //FILTER_NADA
                                            break;

                                        case 2:
                                            diffTex.FilterType = 1;  //FILTER_SAT    
                                            break;

                                        default:
                                            diffTex.FilterType = 0;  //FILTER_PYR
                                            break;
                                    }
                                    diffTex.UVGen.SetFlag(1 << 2, ~(data[filtAddr + 1] | 0xfffffffe));
                                    diffTex.UVGen.SetFlag(1 << 0, ~(data[filtAddr + 1] | 0xfffffffd));
                                    diffTex.UVGen.SetFlag(1 << 3, ~(data[filtAddr + 1] | 0xffffffef));
                                    diffTex.UVGen.SetFlag(1 << 1, ~(data[filtAddr + 1] | 0xffffffdf));

                                    leMat.GetParamBlock(0).SetValue(10, 0, diffTex, 0);
                                    datIndex += 16;     //for some reason zero size extension contains data 00 00 00 00
                                }
                            }
                            break;
                            case (uint)secIDs.REFLECTION_MATERIAL:
                            {
                                datIndex +=8;
                                leMat.GetParamBlock(0).SetValue(12, 0, global.Color.Create(ReadSingle(), ReadSingle(), ReadSingle()), 0);
                                leMat.GetParamBlock(0).SetValue(15, 0, 255.0f*ReadSingle(),0);
                                leMat.GetParamBlock(0).SetValue(17, 0, ReadSingle(), 0);
                                datIndex += 4;  //skip unknown
                            }
                            break;

                            case (uint) secIDs.SPECULAR_MATERIAL:
                            {
                                int secSize = ReadInt32()-4;       //section size-4 because the float read in ReadSingle() takes up 4 bytes
                                datIndex += 4;
                                leMat.GetParamBlock(0).SetValue(16, 0, 255.0f*ReadSingle(), 0);
                                IBitmapTex diffTex = (IBitmapTex)ip.CreateInstance(SClass_ID.Texmap, global.Class_ID.Create((uint)BuiltInClassIDA.BMTEX_CLASS_ID, 0));
                                diffTex.Name= ReadString(ref secSize);  //-4 because the float read in ReadSingle() takes up 4 bytes
                                //MessageBox.Show(diffTex.Name);
                                //MessageBox.Show(datIndex.ToString());
                                leMat.GetParamBlock(0).SetValue(13, 0, diffTex, 0);
                            }
                            break;
                            default:
                                datIndex -= 4;
                            break;
                        }
                    }
                }
                else
                    datIndex -= 4;
             
            }
            // skip extension
            if (ReadUInt32() == (uint)secIDs.EXTENSION)
            {
                int temp= ReadInt32();
                datIndex += temp+4;
            }
            else
                datIndex -= 4;
        }

        private System.UInt32 ReadUInt32()
        {
            this.datIndex += 4;
            return BitConverter.ToUInt32(data, datIndex - 4);
        }

        private System.Int32 ReadInt32()
        {
            this.datIndex += 4;
            return BitConverter.ToInt32(data, datIndex - 4);
        }

        private System.UInt16 ReadUInt16()
        {
            this.datIndex += 2;
            return BitConverter.ToUInt16(data, datIndex - 2);
        }

        private System.String ReadString(ref int size)
        {
            this.datIndex += size;
            return System.Text.ASCIIEncoding.ASCII.GetString(data, datIndex - size, size);
        }

        private System.Single ReadSingle()
        {
            this.datIndex += 4;
            return BitConverter.ToSingle(data, datIndex - 4);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            MessageBox.Show("CODING AND DESIGN\n--------------------------\nSeggaeman\n\n\nCONTRIBUTORS\n--------------------\nDexX (dff format information)\nfastman92 (testing and encouragement)\nREspawn (dff format information)\nKam (gta_material.ms)\ngtamodding.com\n\n\nCopyright ©  2011 by Seggaeman");
        }
    }
}
