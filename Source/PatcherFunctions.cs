﻿using System.Collections.Generic;
using System;
using System.IO;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Diagnostics;
using UniversalPatcher;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Globalization;
using UniversalPatcher.Properties;
using System.Linq;
using System.Drawing;
using System.Xml.Serialization;
using MathParserTK;
using System.Text;


public class upatcher
{
    public class DetectRule
    {
        public DetectRule() { }

        public string xml { get; set; }
        public ushort group { get; set; }
        public string grouplogic { get; set; }   //and, or, xor
        public string address { get; set; }
        public UInt64 data { get; set; }
        public string compare { get; set; }        //==, <, >, !=      
        public string hexdata { get; set; }

        public DetectRule ShallowCopy()
        {
            return (DetectRule)this.MemberwiseClone();
        }
    }


    public class XmlPatch
    {
        public XmlPatch() { }
        public string Name { get; set; }
        public string Description { get; set; }
        public string XmlFile { get; set; }
        public string Segment { get; set; }
        public string CompatibleOS { get; set; }
        public string Data { get; set; }
        public string Rule { get; set; }
        public string HelpFile { get; set; }
        public string PostMessage { get; set; }
    }

    public class Patch
    {
        public Patch()
        {
            patches = new List<XmlPatch>();
        }
        public string Name { get; set; }
        public List<XmlPatch> patches { get; set; }
    }

    public class CVN
    {
        public CVN() { }
        public string XmlFile { get; set; }
        public string AlternateXML { get; set; }
        public string SegmentNr { get; set; }
        public string PN { get; set; }
        public string Ver { get; set; }
        public string cvn { get; set; }
        public string ReferenceCvn { get; set; }

        public CVN ShallowCopy()
        {
            return (CVN)this.MemberwiseClone();
        }
    }
    public struct Block
    {
        public uint Start;
        public uint End;
    }

    public struct CheckWord
    {
        public string Key;
        public uint Address;
    }

    /*
     File information is read in 3 phases:
     1. XML-file have definitions, how info is stored (SegmentConfig)
     2. Addresses for information is collected from file (SegmentAddressData). 
        For example (OS 12579405): read EngineCal segment address from address 514 => SegmentBlocks => Block1 = 8000 - 15DFFF
        PNAddr is segment address +4 => PNaddr = 8004
     3. Read information from file using collected addresses (SegmentInfo)     
     */
    public struct SegmentAddressData
    {
        public List<Block> SegmentBlocks;
        public List<Block> SwapBlocks;
        public List<Block> CS1Blocks;
        public List<Block> CS2Blocks;
        public List<Block> ExcludeBlocks;
        public AddressData CS1Address;
        public AddressData CS2Address;
        public AddressData PNaddr;
        public AddressData VerAddr;
        public AddressData SegNrAddr;
        public List<CheckWord> Checkwords;
        public List<AddressData> ExtraInfo;
    }


    public class DtcCode
    {
        public DtcCode()
        {
            codeInt = 0;
            codeAddrInt = uint.MaxValue;
            //CodeAddr = "";
            statusAddrInt = uint.MaxValue;
            //StatusAddr = "";
            Description = "";
            Status = 99;
            MilStatus = 99;
            MilAddr = "";
            milAddrInt = 0;
            StatusTxt = "";
        }
        public UInt16 codeInt;
        public string Code { get; set; }
        public byte Status { get; set; }
        public string StatusTxt { get; set; }
        public byte MilStatus { get; set; }
        public string Description { get; set; }
        public string Values { get; set; }
        public uint codeAddrInt;
        public string CodeAddr
        {
            get
            {
                if (codeAddrInt == uint.MaxValue)
                    return "";
                else
                    return codeAddrInt.ToString("X8");
            }
            set
            {
                if (value.Length > 0)
                {
                    UInt32 prevVal = codeAddrInt;
                    if (!HexToUint(value, out codeAddrInt))
                        codeAddrInt = prevVal;
                }
                else
                {
                    codeAddrInt = uint.MaxValue;
                }
            }
        }

        public uint statusAddrInt;
        public string StatusAddr
        {
            get
            {
                if (statusAddrInt == uint.MaxValue)
                    return "";
                else
                    return statusAddrInt.ToString("X8");
            }
            set
            {
                if (value.Length > 0)
                {
                    UInt32 prevVal = statusAddrInt;
                    if (!HexToUint(value, out statusAddrInt))
                        statusAddrInt = prevVal;
                }
                else
                {
                    statusAddrInt = uint.MaxValue;
                }
            }
        }
        public uint milAddrInt;
        public string MilAddr { get; set; }
    }

    public class OBD2Code
    {
        public OBD2Code()
        {

        }
        public string Code { get; set; }
        public string Description { get; set; }
    }

    public class TableView
    {
        public TableView()
        { }
        public uint Row { get; set; }
        public uint addrInt;
        public string Address { get; set; }
        public byte dataInt;
        public string Data { get; set; }
    }

    public class SwapSegment
    {
        //For storing information about extracted calibration segments
        public SwapSegment()
        {

        }
        public string FileName { get; set; }
        public string XmlFile { get; set; }
        public string OS { get; set; }
        public string PN { get; set; }
        public string Ver { get; set; }
        public string SegNr { get; set; }
        public int SegIndex { get; set; }
        public string Size { get; set; }
        public string Address { get; set; }
        public string Description { get; set; }
        public string Stock { get; set; }
        public string Cvn { get; set; }
        public string SegmentSizes { get; set; }     //For OS compatibility
        public string SegmentAddresses { get; set; } //For OS compatibility

    }
    public struct AddressData
    {
        public string Name;
        public uint Address;
        public ushort Bytes;
        public AddressDataType Type;
    }

    public struct referenceCvn
    {
        public string PN;
        public string CVN;
    }

    public class FileType
    {
        public FileType() { }

        public string Description { get; set; }
        public string Extension { get; set; }
        public bool Default { get; set; }

        public FileType ShallowCopy()
        {
            return (FileType)this.MemberwiseClone();
        }
    }

    public struct SearchedAddress
    {
        public uint Addr;
        public ushort Rows;
        public ushort Columns;
    }

    /*    public const short CSMethod_None = 0;
        public const short CSMethod_crc16 = 1;
        public const short CSMethod_crc32 = 2;
        public const short CSMethod_Bytesum = 3;
        public const short CSMethod_Wordsum = 4;
        public const short CSMethod_Dwordsum = 5;
        public const short CSMethod_BoschInv = 6;
    */

    public enum CSMethod
    {
        None = 0,
        crc16 = 1,
        crc32 = 2,
        Bytesum = 3,
        Wordsum = 4,
        Dwordsum = 5,
        BoschInv = 6,
        Unknown = 99
    }

    public static List<DetectRule> DetectRules;
    public static List<XmlPatch> PatchList;
    public static List<Patch> patches;
    public static List<CVN> StockCVN;
    public static List<CVN> ListCVN;
    public static List<CVN> BadCvnList;
    public static List<StaticSegmentInfo> SegmentList;
    public static List<StaticSegmentInfo> BadChkFileList = new List<StaticSegmentInfo>();
    public static List<SwapSegment> SwapSegments;
    //public static List<TableSearchConfig> tableSearchConfig;
    public static List<TableSearchResult> tableSearchResult;
    public static List<TableSearchResult> tableSearchResultNoFilters;
    public static List<TableView> tableViews;
    public static List<referenceCvn> referenceCvnList;
    public static List<FileType> fileTypeList;
    public static List<OBD2Code> OBD2Codes;
    public static List<DtcSearchConfig> dtcSearchConfigs;
    public static List<PidSearchConfig> pidSearchConfigs;
    public static List<TableSeek> tableSeeks;
    public static List<SegmentSeek> segmentSeeks;
    public static List<UniversalPatcher.TableData> XdfElements;
    public static List<Units> unitList;
    public static List<RichTextBox> LogReceivers;

    public static string tableSearchFile;
    //public static string tableSeekFile = "";
    public static MathParser parser = new MathParser();
    public static SavingMath savingMath = new SavingMath();

    public static FrmPatcher frmpatcher;
    private static frmSplashScreen frmSplash = new frmSplashScreen();

    //public static string[] dtcStatusCombined = { "MIL and reporting off", "Type A/no MIL", "Type B/no MIL", "Type C/no MIL", "Not reported/MIL", "Type A/MIL", "Type B/MIL", "Type C/MIL" };
    //public static string[] dtcStatus = { "1 Trip/immediately", "2 Trips", "Store only", "Disabled" };


    public static string selectedCompareBin;

    public enum AddressDataType
    {
        Float = 1,
        Int = 2,
        Hex = 3,
        Text = 4,
        Flag = 5,
        Filename = 10
    }

    public enum OutDataType
    {
        Float = 1,
        Int = 2,
        Hex = 3,
        Text = 4,
        Flag = 5,
        Filename = 10
    }

    public enum InDataType
    {
        UBYTE,              //UNSIGNED INTEGER - 8 BIT
        SBYTE,              //SIGNED INTEGER - 8 BIT
        UWORD,              //UNSIGNED INTEGER - 16 BIT
        SWORD,              //SIGNED INTEGER - 16 BIT
        UINT32,              //UNSIGNED INTEGER - 32 BIT
        INT32,              //SIGNED INTEGER - 32 BIT
        UINT64,           //UNSIGNED INTEGER - 64 BIT
        INT64,            //SIGNED INTEGER - 64 BIT
        FLOAT32,       //SINGLE PRECISION FLOAT - 32 BIT
        FLOAT64,        //DOUBLE PRECISION FLOAT - 64 BIT
        UNKNOWN
    }

    public enum TableValueType
    {
        boolean,
        selection,
        bitmask,
        number
    }

    public static void StartupSettings()
    {
        LogReceivers = new List<RichTextBox>();
        tableSeeks = new List<TableSeek>();
        segmentSeeks = new List<SegmentSeek>();
        if (!Directory.Exists(Path.Combine(Application.StartupPath, "Patches")))
            Directory.CreateDirectory(Path.Combine(Application.StartupPath, "Patches"));
        if (!Directory.Exists(Path.Combine(Application.StartupPath, "XML")))
            Directory.CreateDirectory(Path.Combine(Application.StartupPath, "XML"));
        if (!Directory.Exists(Path.Combine(Application.StartupPath, "Segments")))
            Directory.CreateDirectory(Path.Combine(Application.StartupPath, "Segments"));
        if (!Directory.Exists(Path.Combine(Application.StartupPath, "Log")))
            Directory.CreateDirectory(Path.Combine(Application.StartupPath, "Log"));
        if (!Directory.Exists(Path.Combine(Application.StartupPath, "Tuner")))
            Directory.CreateDirectory(Path.Combine(Application.StartupPath, "Tuner"));

        if (UniversalPatcher.Properties.Settings.Default.LastXMLfolder == "")
            UniversalPatcher.Properties.Settings.Default.LastXMLfolder = Path.Combine(Application.StartupPath, "XML");
        if (UniversalPatcher.Properties.Settings.Default.LastPATCHfolder == "")
            UniversalPatcher.Properties.Settings.Default.LastPATCHfolder = Path.Combine(Application.StartupPath, "Patches");

        frmSplash.Show();
        //System.Drawing.Point xy = new Point((int)(this.Location.X + 300), (int)(this.Location.Y + 150));
        Screen myScreen = Screen.FromPoint(Control.MousePosition);
        System.Drawing.Rectangle area = myScreen.WorkingArea;
        Point xy = new Point(area.Width / 2 - 115, area.Height / 2 - 130);
        frmSplash.moveMe(xy);
        frmSplash.labelProgress.Text = "";
        loadSettingFiles();
        frmSplash.Dispose();
    }

    private static void ShowSplash(string txt, bool newLine = true)
    {
        frmSplash.labelProgress.Text += txt;
        if (newLine)
            frmSplash.labelProgress.Text += Environment.NewLine;
    }

    private static void loadSettingFiles()
    {
        DetectRules = new List<DetectRule>();
        StockCVN = new List<CVN>();
        fileTypeList = new List<FileType>();
        dtcSearchConfigs = new List<DtcSearchConfig>();
        pidSearchConfigs = new List<PidSearchConfig>();
        SwapSegments = new List<SwapSegment>();
        unitList = new List<Units>();
        patches = new List<Patch>();
        //Dirty fix to make system work without stockcvn.xml
        CVN ctmp = new CVN();
        ctmp.cvn = "";
        ctmp.PN = "";
        StockCVN.Add(ctmp);

        Logger("Loading configurations... filetypes", false);
        ShowSplash("Loading configurations...");
        ShowSplash("filetypes");
        Application.DoEvents();

        string FileTypeListFile = Path.Combine(Application.StartupPath, "XML", "filetypes.xml");
        if (File.Exists(FileTypeListFile))
        {
            Debug.WriteLine("Loading filetypes.xml");
            System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(List<FileType>));
            System.IO.StreamReader file = new System.IO.StreamReader(FileTypeListFile);
            fileTypeList = (List<FileType>)reader.Deserialize(file);
            file.Close();

        }

        Logger(",dtcsearch", false);
        ShowSplash("dtcsearch");
        Application.DoEvents();
        string CtsSearchFile = Path.Combine(Application.StartupPath, "XML", "DtcSearch.xml");
        if (File.Exists(CtsSearchFile))
        {
            Debug.WriteLine("Loading DtcSearch.xml");
            System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(List<DtcSearchConfig>));
            System.IO.StreamReader file = new System.IO.StreamReader(CtsSearchFile);
            dtcSearchConfigs = (List<DtcSearchConfig>)reader.Deserialize(file);
            file.Close();

        }

        Logger(",pidsearch", false);
        ShowSplash("pidsearch");
        Application.DoEvents();

        string pidSearchFile = Path.Combine(Application.StartupPath, "XML", "PidSearch.xml");
        if (File.Exists(pidSearchFile))
        {
            Debug.WriteLine("Loading PidSearch.xml");
            System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(List<PidSearchConfig>));
            System.IO.StreamReader file = new System.IO.StreamReader(pidSearchFile);
            pidSearchConfigs = (List<PidSearchConfig>)reader.Deserialize(file);
            file.Close();

        }

        Logger(",autodetect", false);
        ShowSplash("autodetect");
        Application.DoEvents();

        string AutoDetectFile = Path.Combine(Application.StartupPath, "XML", "autodetect.xml");
        if (File.Exists(AutoDetectFile))
        {
            Debug.WriteLine("Loading autodetect.xml");
            System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(List<DetectRule>));
            System.IO.StreamReader file = new System.IO.StreamReader(AutoDetectFile);
            DetectRules = (List<DetectRule>)reader.Deserialize(file);
            file.Close();
        }

        Logger(",extractedsegments", false);
        ShowSplash("extractedsegments");
        Application.DoEvents();

        string SwapSegmentListFile = Path.Combine(Application.StartupPath, "Segments", "extractedsegments.xml");
        if (File.Exists(SwapSegmentListFile))
        {
            Debug.WriteLine("Loading extractedsegments.xml");
            System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(List<SwapSegment>));
            System.IO.StreamReader file = new System.IO.StreamReader(SwapSegmentListFile);
            SwapSegments = (List<SwapSegment>)reader.Deserialize(file);
            file.Close();

        }

        Logger(",units", false);
        ShowSplash("units");
        Application.DoEvents();

        string unitsFile = Path.Combine(Application.StartupPath, "Tuner", "units.xml");
        if (File.Exists(unitsFile))
        {
            Debug.WriteLine("Loading units.xml");
            System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(List<Units>));
            System.IO.StreamReader file = new System.IO.StreamReader(unitsFile);
            unitList = (List<Units>)reader.Deserialize(file);
            file.Close();

        }

        Logger(",stockcvn", false);
        ShowSplash("stockcvn");
        Application.DoEvents();

        string StockCVNFile = Path.Combine(Application.StartupPath, "XML", "stockcvn.xml");
        if (File.Exists(StockCVNFile))
        {
            Debug.WriteLine("Loading stockcvn.xml");
            System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(List<CVN>));
            System.IO.StreamReader file = new System.IO.StreamReader(StockCVNFile);
            StockCVN = (List<CVN>)reader.Deserialize(file);
            file.Close();
        }
        loadReferenceCvn();

        Logger(" - Done");
        ShowSplash("Done");

    }

    public static TableValueType getValueType(TableData td)
    {
        TableValueType retVal;

        if (td.Units == null)
            td.Units = "";
        if (td.BitMask != null && td.BitMask.Length > 0)
        {
            retVal = TableValueType.bitmask;
        }
        else if (td.Units.ToLower().Contains("boolean") || td.Units.ToLower().Contains("t/f"))
        {
            retVal = TableValueType.boolean;
        }
        else if (td.Units.ToLower().Contains("true") && td.Units.ToLower().Contains("false"))
        {
            retVal = TableValueType.boolean;
        }
        else if (td.Values.StartsWith("Enum: "))
        {
            retVal = TableValueType.selection;
        }
        else
        {
            retVal = TableValueType.number;
        }
        return retVal;
    }

    public static int getBits(InDataType dataType)
    {
        int bits = 8; // Assume one byte if not defined. OK?
        if (dataType == InDataType.SBYTE || dataType == InDataType.UBYTE)
            bits = 8;
        if (dataType == InDataType.SWORD || dataType == InDataType.UWORD)
            bits = 16;
        if (dataType == InDataType.INT32 || dataType == InDataType.UINT32 || dataType == InDataType.FLOAT32)
            bits = 32;
        if (dataType == InDataType.INT64 || dataType == InDataType.UINT64 || dataType == InDataType.FLOAT64)
            bits = 64;
        if (dataType == InDataType.UNKNOWN)
            Logger("Warning, unknown data type. Assuming UBYTE");

        return bits;
    }
    public static int getElementSize(InDataType dataType)
    {
        int bytes = 1; // Assume one byte if not defined. OK?
        if (dataType == InDataType.SBYTE || dataType == InDataType.UBYTE)
            bytes = 1;
        if (dataType == InDataType.SWORD || dataType == InDataType.UWORD)
            bytes = 2;
        if (dataType == InDataType.INT32 || dataType == InDataType.UINT32 || dataType == InDataType.FLOAT32)
            bytes = 4;
        if (dataType == InDataType.INT64 || dataType == InDataType.UINT64 || dataType == InDataType.FLOAT64)
            bytes = 8;
        if (dataType == InDataType.UNKNOWN)
            Logger("Warning, unknown data type. Assuming UBYTE");

        return bytes;
    }
    public static bool getSigned(InDataType dataType)
    {
        bool signed = false;
        if (dataType == InDataType.INT32 || dataType == InDataType.INT64 || dataType == InDataType.SBYTE || dataType == InDataType.SWORD)
            signed = true;
        return signed;
    }

    public static InDataType convertToDataType(string bitStr, bool signed, bool floating)
    {
        InDataType retVal = InDataType.UNKNOWN;
        int bits = Convert.ToInt32(bitStr);
        retVal = convertToDataType(bits / 8, signed, floating);
        return retVal;
    }

    public static InDataType convertToDataType(int elementSize, bool Signed, bool floating)
    {
        InDataType DataType = InDataType.UNKNOWN; 
        if (elementSize == 1)
        {
            if (Signed == true)
            {
                DataType = InDataType.SBYTE;
            }
            else
            {
                DataType = InDataType.UBYTE;
            }

        }
        else if (elementSize == 2)
        {
            if (Signed == true)
            {
                DataType = InDataType.SWORD;
            }
            else
            {
                DataType = InDataType.UWORD;
            }

        }
        else if (elementSize == 4)
        {
            if (floating)
            {
                DataType = InDataType.FLOAT32;
            }
            else
            {
                if (Signed == true)
                {
                    DataType = InDataType.UINT32;
                }
                else
                {
                    DataType = InDataType.INT32;
                }
            }
        }
        else if (elementSize == 8)
        {
            if (floating)
            {
                DataType = InDataType.FLOAT64;
            }
            else
            {
                if (Signed == true)
                {
                    DataType = InDataType.INT64;
                }
                else
                {
                    DataType = InDataType.UINT64;
                }
            }

        }
        return DataType;
    }

    public static double getMaxValue (InDataType dType)
    {
        if (dType == InDataType.FLOAT32)
            return float.MaxValue;
        else if (dType == InDataType.FLOAT64)
            return double.MaxValue;
        else if (dType == InDataType.INT32)
            return Int32.MaxValue;
        else if (dType == InDataType.INT64)
            return Int64.MaxValue;
        else if (dType == InDataType.SBYTE)
            return sbyte.MaxValue;
        else if (dType == InDataType.SWORD)
            return Int16.MaxValue;
        else if (dType == InDataType.UBYTE)
            return byte.MaxValue;
        else if (dType == InDataType.UINT32)
            return UInt32.MaxValue;
        else if (dType == InDataType.UINT64)
            return UInt64.MaxValue;
        else if (dType == InDataType.UWORD)
            return UInt16.MaxValue;
        else 
            return byte.MaxValue;

    }

    public static double getMinValue(InDataType dType)
    {
        if (dType == InDataType.FLOAT32)
            return float.MinValue;
        else if (dType == InDataType.FLOAT64)
            return double.MinValue;
        else if (dType == InDataType.INT32)
            return Int32.MinValue;
        else if (dType == InDataType.INT64)
            return Int64.MinValue;
        else if (dType == InDataType.SBYTE)
            return sbyte.MinValue;
        else if (dType == InDataType.SWORD)
            return Int16.MinValue;
        else if (dType == InDataType.UBYTE)
            return byte.MinValue;
        else if (dType == InDataType.UINT32)
            return UInt32.MinValue;
        else if (dType == InDataType.UINT64)
            return UInt64.MinValue;
        else if (dType == InDataType.UWORD)
            return UInt16.MinValue;
        else
            return byte.MinValue;

    }

    public static string readConversionTable(string mathStr, PcmFile PCM)
    {
        //Example: TABLE:'MAF Scalar #1'
        string retVal = mathStr;
        int start = mathStr.IndexOf("table:") + 6;
        int mid = mathStr.IndexOf("'", start + 7);
        string conversionTable = mathStr.Substring(start, mid - start + 1);
        TableData tmpTd = new TableData();
        tmpTd.TableName = conversionTable.Replace("'", "");
        int targetId = findTableDataId(tmpTd, PCM.tableDatas);
        if (targetId > -1)
        {
            TableData conversionTd = PCM.tableDatas[targetId];
            double conversionVal = getValue(PCM.buf, (uint)(conversionTd.addrInt + conversionTd.Offset), conversionTd, 0, PCM);
            retVal = mathStr.Replace("table:" + conversionTable, conversionVal.ToString());
            Debug.WriteLine("Using conversion table: " + conversionTd.TableName);
        }

        return retVal;
    }

    public static string readConversionRaw(string mathStr, PcmFile PCM)
    {
        // Example: RAW:0x321:SWORD:MSB
        string retVal = mathStr;
        int start = mathStr.IndexOf("raw:");
        int mid = mathStr.IndexOf(" ", start + 3);
        string rawStr = mathStr.Substring(start, mid - start + 1);
        string[] rawParts = rawStr.Split(':');
        if (rawParts.Length < 3)
        {
            throw new Exception("Unknown RAW definition in Math: " + mathStr);
        }
        InDataType idt =(InDataType) Enum.Parse(typeof(InDataType), rawParts[2].ToUpper());
        TableData tmpTd = new TableData();
        tmpTd.Address = rawParts[1];
        tmpTd.Offset = 0;
        tmpTd.DataType = idt;
        double rawVal = (double)getRawValue(PCM.buf, tmpTd.addrInt, tmpTd, 0,PCM.platformConfig.MSB);
        if (rawParts.Length > 3 && rawParts[3].StartsWith("lsb"))
        {
            int eSize = getElementSize(idt);
            rawVal = SwapBytes((UInt64)rawVal,eSize);
        }
        retVal = mathStr.Replace(rawStr, rawVal.ToString());
        return retVal;
    }

    //
    //Get value from defined table, using defined math functions.
    //
    public static double getValue(byte[] myBuffer, uint addr, TableData mathTd, uint offset, PcmFile PCM)
    {
        double retVal = 0;
        try
        {

            if (mathTd.OutputType == OutDataType.Flag && mathTd.BitMask != null && mathTd.BitMask.Length > 0)
            {
                UInt64 rawVal = (UInt64)getRawValue(myBuffer, addr, mathTd, offset,PCM.platformConfig.MSB);
                UInt64 mask = Convert.ToUInt64(mathTd.BitMask.Replace("0x", ""), 16);
                if (((UInt64) rawVal & mask) == mask)
                    return 1;
                else
                    return 0;
            }

            UInt32 bufAddr = addr - offset;

            if (mathTd.DataType == InDataType.SBYTE)
                retVal = (sbyte)myBuffer[bufAddr];
            if (mathTd.DataType == InDataType.UBYTE)
                retVal = myBuffer[bufAddr];
            if (mathTd.DataType == InDataType.SWORD)
                retVal = readInt16(myBuffer, bufAddr, PCM.platformConfig.MSB);
            if (mathTd.DataType == InDataType.UWORD)
                retVal = readUint16(myBuffer, bufAddr, PCM.platformConfig.MSB);
            if (mathTd.DataType == InDataType.INT32)
                retVal = readInt32(myBuffer, bufAddr, PCM.platformConfig.MSB);
            if (mathTd.DataType == InDataType.UINT32)
                retVal = readUint32(myBuffer, bufAddr, PCM.platformConfig.MSB);
            if (mathTd.DataType == InDataType.INT64)
                retVal = readInt64(myBuffer, bufAddr, PCM.platformConfig.MSB);
            if (mathTd.DataType == InDataType.UINT64)
                retVal = readUint64(myBuffer, bufAddr, PCM.platformConfig.MSB);
            if (mathTd.DataType == InDataType.FLOAT32)
                retVal = readFloat32(myBuffer, bufAddr, PCM.platformConfig.MSB);
            if (mathTd.DataType == InDataType.FLOAT64)
                retVal = readFloat64(myBuffer, bufAddr, PCM.platformConfig.MSB);

            if (mathTd.Math == null || mathTd.Math.Length == 0)
                mathTd.Math = "X";
            string mathStr = mathTd.Math.ToLower().Replace("x", retVal.ToString());
            if (mathStr.Contains("table:"))
            {
                mathStr = readConversionTable(mathStr, PCM);
            }
            if (mathStr.Contains("raw:"))
            {
                mathStr = readConversionRaw(mathStr, PCM);
            }
            retVal = parser.Parse(mathStr, false);
            //Debug.WriteLine(mathStr);
        }
        catch (Exception ex)
        {
            var st = new StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(st.FrameCount - 1);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            LoggerBold("Patcherfunctions error, line " + line + ": " + ex.Message);
        }

        return retVal;
    }

    public static double getRawValue(byte[] myBuffer, UInt32 addr, TableData mathTd, uint offset, bool MSB)
    {
        UInt32 bufAddr = addr - offset;
        double retVal = 0;
        try
        {
            switch (mathTd.DataType)
            {
                case InDataType.SBYTE:
                    return (sbyte)myBuffer[bufAddr];
                case InDataType.UBYTE:
                    return (byte)myBuffer[bufAddr];
                case InDataType.SWORD:
                    return (Int16)readInt16(myBuffer, bufAddr, MSB);
                case InDataType.UWORD:
                    return (UInt16)readUint16(myBuffer, bufAddr, MSB);
                case InDataType.INT32:
                    return (Int32)readInt32(myBuffer, bufAddr, MSB);
                case InDataType.UINT32:
                    return (UInt32)readUint32(myBuffer, bufAddr, MSB);
                case InDataType.INT64:
                    return (Int64)readInt64(myBuffer, bufAddr, MSB);
                case InDataType.UINT64:
                    return (UInt64)readInt64(myBuffer, bufAddr, MSB);
                case InDataType.FLOAT32:
                    return (float)readFloat32(myBuffer, bufAddr, MSB);
                case InDataType.FLOAT64:
                    return readFloat64(myBuffer, bufAddr, MSB);
            }

        }
        catch (Exception ex)
        {
            var st = new StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(st.FrameCount - 1);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            LoggerBold("Patcherfunctions error, line " + line + ": " + ex.Message);
        }

        return retVal;
    }


    public static  Dictionary<double, string> parseEnumHeaders(string eVals)
    {
        if (eVals.ToLower().StartsWith("enum:"))
            eVals = eVals.Substring(5).Trim();
        Dictionary<double, string> retVal = new Dictionary<double, string>();
        string[] posVals = eVals.Split(',');
        for (int r = 0; r < posVals.Length; r++)
        {
            string[] parts = posVals[r].Split(':');
            double val = 0;
            double.TryParse(parts[0], out val);
            string txt = posVals[r];
            if (!retVal.ContainsKey(val))
                retVal.Add(val, txt);
        }
        retVal.Add(double.MaxValue, "------------");
        return retVal;
    }

    public static Dictionary<int, string> parseIntEnumHeaders(string eVals)
    {
        if (eVals.ToLower().StartsWith("enum:"))
            eVals = eVals.Substring(5).Trim();
        Dictionary<int, string> retVal = new Dictionary<int, string>();
        string[] posVals = eVals.Split(',');
        for (int r = 0; r < posVals.Length; r++)
        {
            string[] parts = posVals[r].Split(':');
            int val = 0;
            int.TryParse(parts[0], out val);
            string txt = posVals[r];
            if (!retVal.ContainsKey(val))
                retVal.Add(val, txt);
        }
        retVal.Add(int.MaxValue, "------------");
        return retVal;
    }

    public static Dictionary<byte, string> parseDtcValues(string eVals)
    {
        if (eVals.ToLower().StartsWith("enum:"))
            eVals = eVals.Substring(5).Trim();
        Dictionary<byte, string> retVal = new Dictionary<byte, string>();
        string[] posVals = eVals.Split(',');
        for (int r = 0; r < posVals.Length; r++)
        {
            string[] parts = posVals[r].Split(':');
            byte val = 0;
            byte.TryParse(parts[0], out val);
            string txt = parts[1];
            if (!retVal.ContainsKey(val))
                retVal.Add(val, txt);
        }
        retVal.Add(byte.MaxValue, "------------");
        return retVal;
    }

    public static uint checkPatchCompatibility(XmlPatch xpatch, PcmFile basefile, bool newline = true)
    {
        uint retVal = uint.MaxValue;
        bool isCompatible = false;
        string[] Parts = xpatch.XmlFile.Split(',');
        foreach (string Part in Parts)
        {
            if (Part.ToLower().Replace(".xml","") == basefile.configFile)
                isCompatible = true;
        }
        if (!isCompatible)
        {
            Logger("Incompatible patch, current configfile: " + basefile.configFile + ", patch configile: " + xpatch.XmlFile.Replace(".xml", ""));
            return retVal;
        }

        if (xpatch.CompatibleOS.ToLower().StartsWith("search:"))
        {
            string searchStr = xpatch.CompatibleOS.Substring(7);
            for (int seg = 0; seg < basefile.segmentinfos.Length; seg++)
            {
                if (basefile.segmentinfos[seg].Name.ToLower() == xpatch.Segment.ToLower())
                {
                    Debug.WriteLine("Searching only segment: " + basefile.segmentinfos[seg].Name);
                    for (int b = 0; b < basefile.segmentAddressDatas[seg].SegmentBlocks.Count; b++)
                    {
                        retVal = searchBytes(basefile, searchStr, basefile.segmentAddressDatas[seg].SegmentBlocks[b].Start, basefile.segmentAddressDatas[seg].SegmentBlocks[b].End);
                        if (retVal < uint.MaxValue)
                            break;
                    }
                }
            }
            if (retVal == uint.MaxValue) //Search whole bin
                retVal = searchBytes(basefile, searchStr, 0, basefile.fsize);
            if (retVal < uint.MaxValue)
            {
                Logger("Data found at address: " + retVal.ToString("X8"));
                isCompatible = true;
            }
            else
            {
                uint tmpVal = searchBytes(basefile, xpatch.Data, 0, basefile.fsize);
                if (tmpVal < uint.MaxValue)
                    Logger("Patch is already applied, data found at: " + tmpVal.ToString("X8"));
                else
                    Logger("Data not found. Incompatible patch");
            }
        }
        else if (xpatch.CompatibleOS.ToLower().StartsWith("table:"))
        {
            if (basefile.tableDatas.Count < 3)
                basefile.loadTunerConfig();
            basefile.importDTC();
            basefile.importSeekTables();
            string[] tableParts = xpatch.CompatibleOS.Split(',');
            if (tableParts.Length < 3)
            {
                Logger("Incomplete table definition:" + xpatch.CompatibleOS);
            }
            else
            {
                string tbName = "";
                int rows = 1;
                int columns = 1;
                for (int i = 0; i < tableParts.Length; i++)
                {
                    string[] xParts = tableParts[i].Split(':');
                    if (xParts[0].ToLower() == "table")
                        tbName = xParts[1];
                    if (xParts[0].ToLower() == "columns")
                        columns = Convert.ToInt32(xParts[1]);
                    if (xParts[0].ToLower() == "rows")
                        rows = Convert.ToInt32(xParts[1]);
                }
                TableData tmpTd = new TableData();
                tmpTd.TableName = tbName;
                Logger("Table: " + tbName,newline);
                int id = findTableDataId(tmpTd, basefile.tableDatas);
                if (id > -1)
                {
                    tmpTd = basefile.tableDatas[id];
                    if (tmpTd.Rows == rows && tmpTd.Columns == columns)
                    {
                        isCompatible = true;
                        retVal = (uint)id;
                    }
                }
            }
        }
        else
        {
            string[] OSlist = xpatch.CompatibleOS.Split(',');
            string BinPN = "";
            foreach (string OS in OSlist)
            {
                Parts = OS.Split(':');
                if (Parts[0] == "ALL")
                {
                    isCompatible = true;
                    if (!HexToUint(Parts[1], out retVal))
                        throw new Exception("Can't decode from HEX: " + Parts[1] + " (" + xpatch.CompatibleOS + ")");
                    Debug.WriteLine("ALL, Addr: " + Parts[1]);
                }
                else
                {
                    if (BinPN == "")
                    {
                        //Search OS once
                        for (int s = 0; s < basefile.Segments.Count; s++)
                        {
                            if (!basefile.Segments[s].Missing)
                            {
                                string PN = basefile.ReadInfo(basefile.segmentAddressDatas[s].PNaddr);
                                if (Parts[0] == PN)
                                {
                                    isCompatible = true;
                                    BinPN = PN;
                                }
                            }
                        }
                    }
                    if (Parts[0] == BinPN)
                    {
                        isCompatible = true;
                        if (!HexToUint(Parts[1], out retVal))
                            throw new Exception("Can't decode from HEX: " + Parts[1] + " (" + xpatch.CompatibleOS + ")");
                        Debug.WriteLine("OS: " + BinPN + ", Addr: " + Parts[1]);
                    }
                }
            }
        }
        return retVal;
    }

    public static uint applyTablePatch(ref PcmFile basefile, XmlPatch xpatch, int tdId)
    {
        int diffCount = 0;
        frmTableEditor frmTE = new frmTableEditor();
        TableData pTd = basefile.tableDatas[tdId];
        frmTE.prepareTable(basefile, pTd, null, "A");
        //frmTE.loadTable();
        uint addr = (uint)(pTd.addrInt + pTd.Offset);
        uint step = (uint)getElementSize(pTd.DataType);
        try
        {
            string[] dataParts = xpatch.Data.Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);
            for (int cell = 0; cell < frmTE.compareFiles[0].tableInfos[0].tableCells.Count; cell++)
            {
                TableCell tCell = frmTE.compareFiles[0].tableInfos[0].tableCells[cell];
                double val = Convert.ToDouble(dataParts[cell].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                if (tCell.saveValue(val))
                    diffCount++;
            }
            //frmTE.saveTable(false);
            Array.Copy(frmTE.compareFiles[0].buf, 0, basefile.buf, frmTE.compareFiles[0].tableBufferOffset, frmTE.compareFiles[0].buf.Length);
            frmTE.Dispose();
        }
        catch (Exception ex)
        {
            var st = new StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(st.FrameCount - 1);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            LoggerBold("Error, patcherfunctions line " + line + ": " + ex.Message);

        }
        return (uint)(diffCount * step);
    }

    public static void applyTdPatch(TableData td, ref PcmFile PCM)
    {
        try
        {
            Logger("Applying patch: " + td.TableName, false);
            string data = td.Values.Substring(7); //Remove "Patch: "
            string[] parts = data.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            int byteCount = 0;
            foreach(string part in parts)
            {
                string[] dParts = part.Split(':');
                if (dParts.Length != 2)
                    throw new Exception(" Data error: " + td.Values);
                uint addr;
                if (!HexToUint(dParts[0], out addr))
                    throw new Exception(" Data error: " + td.Values);

                for (int i = 0; i < dParts[1].Length; i += 2)
                {
                    string byteStr = dParts[1].Substring(i, 2);
                    byte b;
                    if (!HexToByte(byteStr, out b))
                        throw new Exception(" Data error: " + td.Values);
                    if (PCM.buf[addr] != b)
                        byteCount++;
                    PCM.buf[addr] = b;
                    addr++;
                }
            }
            Logger(" [OK]");
            Logger("Modified " + byteCount.ToString() + " bytes");
        }
        catch (Exception ex)
        {
            var st = new StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(st.FrameCount - 1);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            LoggerBold(" Error, applyTdPatch line " + line + ": " + ex.Message);

        }

    }

    public static void applyTdTablePatch(ref PcmFile PCM, TableData patchTd)
    {
        XmlPatch xpatch = new XmlPatch();
        xpatch.CompatibleOS = patchTd.CompatibleOS.TrimStart(',');
        xpatch.Data = patchTd.Values.Substring(11).Trim();
        xpatch.Description = patchTd.TableDescription;
        xpatch.Name = patchTd.TableName;
        xpatch.XmlFile = PCM.configFile;
        Logger("Applying patch...");

        uint ind = checkPatchCompatibility(xpatch, PCM, false);
        if (ind < uint.MaxValue)
        {
            uint bytes = applyTablePatch(ref PCM, xpatch, (int)ind);
            Logger(Environment.NewLine +  "Modified: " + bytes.ToString() + " bytes");
        }
    }

    public static bool ApplyXMLPatch(ref PcmFile basefile)
    {
        try
        {
            string PrevSegment = "";
            uint ByteCount = 0;
            string[] Parts;
            string prevDescr = "";

            Logger("Applying patch:");
            foreach (XmlPatch xpatch in PatchList)
            {
                if (xpatch.Description != null && xpatch.Description != "" && xpatch.Description != prevDescr)
                    Logger(xpatch.Description);
                prevDescr = xpatch.Description;
                if (xpatch.Segment != null && xpatch.Segment.Length > 0 && PrevSegment != xpatch.Segment)
                {
                    PrevSegment = xpatch.Segment;
                    Logger("Segment: " + xpatch.Segment);
                }
                uint Addr = checkPatchCompatibility(xpatch, basefile,false);
                if (Addr < uint.MaxValue)
                {
                    bool PatchRule = true; //If there is no rule, apply patch
                    if (xpatch.Rule != null && (xpatch.Rule.Contains(':') || xpatch.Rule.Contains('[')))
                    {
                        Parts = xpatch.Rule.Split(new char[] { ']', '[', ':' }, StringSplitOptions.RemoveEmptyEntries);
                        if (Parts.Length == 3)
                        {
                            uint RuleAddr;
                            if (!HexToUint(Parts[0], out RuleAddr))
                                throw new Exception("Can't decode from HEX: " + Parts[0] + " (" + xpatch.Rule + ")");
                            ushort RuleMask;
                            if (!HexToUshort(Parts[1], out RuleMask))
                                throw new Exception("Can't decode from HEX: " + Parts[1] + " (" + xpatch.Rule + ")");
                            ushort RuleValue;
                            if (!HexToUshort(Parts[2].Replace("!", ""), out RuleValue))
                                throw new Exception("Can't decode from HEX: " + Parts[2] + " (" + xpatch.Rule + ")");

                            if (Parts[2].Contains("!"))
                            {
                                if ((basefile.buf[RuleAddr] & RuleMask) != RuleValue)
                                {
                                    PatchRule = true;
                                    Logger("Rule match, applying patch");
                                }
                                else
                                {
                                    PatchRule = false;
                                    Logger("Rule doesn't match, skipping patch");
                                }
                            }
                            else
                            {
                                if ((basefile.buf[RuleAddr] & RuleMask) == RuleValue)
                                {
                                    PatchRule = true;
                                    Logger("Rule match, applying patch");
                                }
                                else
                                {
                                    PatchRule = false;
                                    Logger("Rule doesn't match, skipping patch");
                                }
                            }

                        }
                    }
                    if (PatchRule)
                    {
                        if (xpatch.CompatibleOS.ToLower().StartsWith("table:"))
                        {
                            uint bCount = applyTablePatch(ref basefile, xpatch, (int)Addr);
                            Logger(", " + bCount.ToString() + " bytes");
                            ByteCount += bCount;
                        }
                        else
                        {
                            Debug.WriteLine(Addr.ToString("X") + ":" + xpatch.Data);
                            Parts = xpatch.Data.Split(' ');
                            foreach (string Part in Parts)
                            {
                                //Actually add patch data:
                                if (Part.Contains("[") || Part.Contains(":"))
                                {
                                    //Set bits / use Mask
                                    byte Origdata = basefile.buf[Addr];
                                    Debug.WriteLine("Set address: " + Addr.ToString("X") + ", old data: " + Origdata.ToString("X"));
                                    string[] dataparts;
                                    dataparts = Part.Split(new char[] { ']', '[', ':' }, StringSplitOptions.RemoveEmptyEntries);
                                    byte Setdata = byte.Parse(dataparts[0], System.Globalization.NumberStyles.HexNumber);
                                    byte Mask = byte.Parse(dataparts[1].Replace("]", ""), System.Globalization.NumberStyles.HexNumber);

                                    // Set 1
                                    byte tmpMask = (byte)(Mask & Setdata);
                                    byte Newdata = (byte)(Origdata | tmpMask);

                                    // Set 0
                                    tmpMask = (byte)(Mask & ~Setdata);
                                    Newdata = (byte)(Newdata & ~tmpMask);

                                    Debug.WriteLine("New data: " + Newdata.ToString("X"));
                                    basefile.buf[Addr] = Newdata;
                                }
                                else
                                {
                                    //Set byte
                                    if (Part != "*") //Skip wildcards
                                        basefile.buf[Addr] = Byte.Parse(Part, System.Globalization.NumberStyles.HexNumber);
                                }
                                Addr++;
                                ByteCount++;
                            }
                        }
                    }
                    if (xpatch.PostMessage != null && xpatch.PostMessage.Length > 1)
                        LoggerBold(xpatch.PostMessage);
                }
                else
                {
                    Logger("Patch is not compatible");
                    return false;
                }
            }
            Logger("Applied: " + ByteCount.ToString() + " Bytes");
            if (ByteCount > 0)
                Logger("You can save BIN file now");
        }
        catch (Exception ex)
        {
            Logger("Error: " + ex.Message);
            return false;
        }
        return true;
    }

    public static bool compareTables(int id1, int id2, PcmFile pcm1, PcmFile pcm2)
    {
        bool match = true;

        TableData td1 = pcm1.tableDatas[id1];
        TableData td2 = pcm2.tableDatas[id2];

        if ((td1.Rows * td1.Columns) != (td2.Rows * td2.Columns))
            return false;
        List<double> tableValues = new List<double>();
        uint addr = (uint)(td1.addrInt + td1.Offset);
        uint step = (uint)getElementSize(td1.DataType);
        if (td1.RowMajor)
        {
            for (int r = 0; r < td1.Rows; r++)
            {
                for (int c = 0; c < td1.Columns; c++)
                {
                    double val = getValue(pcm1.buf,addr,td1,0,pcm1);
                    tableValues.Add(val);
                    addr += step;
                }
            }
        }
        else
        {
            for (int c = 0; c < td1.Columns; c++)
            {
                for (int r = 0; r < td1.Rows; r++)
                {
                    double val = getValue(pcm1.buf, addr, td1, 0, pcm1);
                    tableValues.Add(val);
                    addr += step;
                }
            }
        }


        addr = (uint)(td2.addrInt + td2.Offset);
        step = (uint)getElementSize(td2.DataType);
        int i = 0;
        if (td2.RowMajor)
        {
            for (int r = 0; r < td2.Rows; r++)
            {
                for (int c = 0; c < td2.Columns; c++)
                {
                    double val = getValue(pcm2.buf, addr, td2, 0,pcm2);
                    if (val != tableValues[i])
                    {
                        match = false;
                        break;
                    }
                    addr += step;
                    i++;
                }
            }
        }
        else
        {
            for (int c = 0; c < td2.Columns; c++)
            {
                for (int r = 0; r < td2.Rows; r++)
                {
                    double val = getValue(pcm2.buf, addr, td2, 0, pcm2);
                    if (val != tableValues[i])
                    {
                        match = false;
                        break;
                    }
                    addr += step;
                    i++;
                }
            }
        }

        return match;
    }

    public static byte[] ReadBin(string FileName)
    {

        uint Length = (uint)new FileInfo(FileName).Length;
        byte[] buf = new byte[Length];

        long offset = 0;
        long remaining = Length;

        using (BinaryReader freader = new BinaryReader(File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
        {
            freader.BaseStream.Seek(0, 0);
            while (remaining > 0)
            {
                int read = freader.Read(buf, (int)offset, (int)remaining);
                if (read <= 0)
                    throw new EndOfStreamException
                        (String.Format("End of stream reached with {0} bytes left to read", remaining));
                remaining -= read;
                offset += read;
            }
            freader.Close();
        }
        return buf;
    }


    public static void WriteBinToFile(string FileName, byte[] Buf)
    {

        using (FileStream stream = new FileStream(FileName, FileMode.Create))
        {
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Buf);
                writer.Close();
            }
        }
    }

    public static void WriteSegmentToFile(string FileName, List<Block> Addr, byte[] Buf)
    {

        using (FileStream stream = new FileStream(FileName, FileMode.Create))
        {
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                for (int b=0;b<Addr.Count;b++)
                {
                    uint StartAddr = Addr[b].Start;
                    uint Length = Addr[b].End - Addr[b].Start + 1;
                    writer.Write(Buf, (int)StartAddr, (int)Length);
                }
                writer.Close();
            }
        }

    }

    public static string ReadTextFile(string fileName)
    {
        StreamReader sr = new StreamReader(fileName);
        string fileContent = sr.ReadToEnd();
        sr.Close();
        return fileContent;
    }

    public static void WriteTextFile(string fileName, string fileContent)
    {
        using (StreamWriter writetext = new StreamWriter(fileName))
        {
            writetext.Write(fileContent);
        }

    }

    public static void SaveTableList(PcmFile PCM, string fName, string compXml)
    {
        try
        {
            string defName = Path.Combine(Application.StartupPath, "Tuner", PCM.OS + ".xml");
            if (PCM.OS.Length == 0)
                defName = Path.Combine(Application.StartupPath, "Tuner", PCM.configFile + "-def.xml");
            if (compXml.Length > 0)
                defName = Path.Combine(Application.StartupPath, "Tuner", compXml);
            if (fName.Length == 0)
                fName = SelectSaveFile("XML Files (*.xml)|*.xml|ALL Files (*.*)|*.*", defName);
            if (fName.Length == 0)
                return;            
            Logger("Saving file " + fName + "...", false);
            PCM.SaveTableList(fName);
            Logger(" [OK]");
        }
        catch (Exception ex)
        {
            var st = new StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(st.FrameCount - 1);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            LoggerBold("Error, frmTuner line " + line + ": " + ex.Message);
        }

    }


    public static void saveOBD2Codes()
    {
        string OBD2CodeFile = Path.Combine(Application.StartupPath, "XML", "OBD2Codes.xml");
        Logger("Saving file " + OBD2CodeFile + "...", false);
        using (FileStream stream = new FileStream(OBD2CodeFile, FileMode.Create))
        {
            System.Xml.Serialization.XmlSerializer writer = new System.Xml.Serialization.XmlSerializer(typeof(List<OBD2Code>));
            writer.Serialize(stream, OBD2Codes);
            stream.Close();
        }
        Logger(" [OK]");

    }

    public static void loadOBD2Codes()
    {
        string OBD2CodeFile = Path.Combine(Application.StartupPath, "XML", "OBD2Codes.xml");
        if (File.Exists(OBD2CodeFile))
        {
            Debug.WriteLine("Loading OBD2Codes.xml");
            System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(List<OBD2Code>));
            System.IO.StreamReader file = new System.IO.StreamReader(OBD2CodeFile);
            OBD2Codes = (List<OBD2Code>)reader.Deserialize(file);
            file.Close();
        }
        else
        {
            OBD2Codes = new List<OBD2Code>();
        }

    }

    public static string autoDetect(PcmFile PCM)
    {
        AutoDetect autod = new AutoDetect();
        return autod.autoDetect(PCM);
    }

    public static UInt64 CalculateChecksum(bool MSB, byte[] Data, AddressData CSAddress, List<Block> CSBlocks,List<Block> ExcludeBlocks, CSMethod Method, short Complement, ushort Bytes, Boolean SwapB, bool dbg=true)
    {
        UInt64 sum = 0;
        try
        {
            if (Method == CSMethod.None)
                return UInt64.MaxValue;
            if (dbg)
                Debug.WriteLine("Calculating checksum, method: " + Method);
            uint BufSize = 0;
            List<Block> Blocks = new List<Block>();

            if (Method == CSMethod.BoschInv)
            {
                UInt64 sum1 = 0;
                UInt64 sum2 = 0;
                for (int p = 0; p < CSBlocks.Count; p++)
                {
                    Block bl = CSBlocks[p];

                    for (uint a = bl.Start; a < bl.End; a += 2)
                    {
                        uint val = readUint16(Data, a, MSB);
                        sum2 += val;
                        if (a < (CSAddress.Address - 1) || a > (CSAddress.Address + CSAddress.Bytes))
                            sum1 += val;
                    }
                    Debug.WriteLine("sum1: " + sum1.ToString("X") + ", sum2: " + sum2.ToString("X"));

                    if (CSAddress.Address >= bl.Start && CSAddress.Address < bl.End)
                    {
                        //Checksum address inside of range
                        sum = 0x1FFFE + sum1 ;
                        if (MSB)
                            sum = (sum << 32) + (0xFFFFFFFF - sum);
                        else
                            sum = ((0xFFFFFFFF - sum)<<32) + sum;
                        Debug.WriteLine("sum: " + sum.ToString("X"));
                    }
                    else
                    {
                        if (MSB)
                            sum = (sum2 << 32) + (0xFFFFFFFF - sum2);
                        else
                            sum = ((0xFFFFFFFF - sum2) << 32) + sum2;
                        Debug.WriteLine("sum: " + sum.ToString("X"));
                    }
                }
            }
            else
            {
                for (int p = 0; p < CSBlocks.Count; p++)
                {
                    Block B = new Block();
                    B.Start = CSBlocks[p].Start;
                    B.End = CSBlocks[p].End;
                    if (CSAddress.Address >= B.Start && CSAddress.Address <= B.End)
                    {
                        //Checksum  is located in this block
                        if (CSAddress.Address == B.Start)    //At beginning of segment
                        {
                            //At beginning of segment
                            if (dbg)
                                Debug.WriteLine("Checksum is at start of block, skipping");
                            B.Start += CSAddress.Bytes;
                        }
                        else
                        {
                            //Located at middle of block, create new block C, ending before checksum
                            if (dbg)
                                Debug.WriteLine("Checksum is at middle of block, skipping");
                            Block C = new Block();
                            C.Start = B.Start;
                            C.End = CSAddress.Address - 1;
                            Blocks.Add(C);
                            BufSize += C.End - C.Start + 1;
                            B.Start = CSAddress.Address + CSAddress.Bytes; //Move block B to start after Checksum
                        }
                    }
                    foreach (Block ExcBlock in ExcludeBlocks)
                    {
                        if (ExcBlock.Start >= B.Start && ExcBlock.End <= B.End)
                        {
                            //Excluded block in this block
                            if (ExcBlock.Start == B.Start)    //At beginning of segment, move start of block
                            {
                                B.Start = ExcBlock.End + 1;
                            }
                            else
                            {
                                if (ExcBlock.End < B.End - 1)
                                {
                                    //Located at middle of block, create new block C, ending before excluded block
                                    Block C = new Block();
                                    C.Start = B.Start;
                                    C.End = ExcBlock.Start - 1;
                                    Blocks.Add(C);
                                    BufSize += C.End - C.Start + 1;
                                    B.Start = ExcBlock.End + 1; //Move block B to start after excluded block
                                }
                                else
                                {
                                    //Exclude block at end of block, move end of block backwards
                                    B.End = ExcBlock.Start - 1;
                                }
                            }
                        }
                    }
                    Blocks.Add(B);
                    BufSize += B.End - B.Start + 1;
                }

                byte[] tmp = new byte[BufSize];
                uint Offset = 0;
                foreach (Block B in Blocks)
                {
                    //Copy blocks to tmp array for calculation
                    if (dbg)
                        Debug.WriteLine("Block: " + B.Start.ToString("X") + " - " + B.End.ToString("X"));
                    uint BlockSize = B.End - B.Start + 1;
                    Array.Copy(Data, B.Start, tmp, Offset, BlockSize);
                    Offset += BlockSize;
                }

                switch (Method)
                {
                    case CSMethod.Bytesum:
                        for (uint i = 0; i < tmp.Length; i++)
                        {
                            sum += tmp[i];
                        }
                        break;

                    case CSMethod.Wordsum:
                        for (uint i = 0; i < tmp.Length - 1; i += 2)
                        {
                            sum += readUint16(tmp, i, MSB);
                        }
                        break;

                    case CSMethod.Dwordsum:
                        for (uint i = 0; i < tmp.Length - 3; i += 4)
                        {
                            sum += readUint32(tmp, i, MSB);
                        }
                        break;

                    case CSMethod.crc16:
                        Crc16 C16 = new Crc16();
                        sum = C16.ComputeChecksum(tmp);
                        break;

                    case CSMethod.crc32:
                        Crc32 C32 = new Crc32();
                        sum = C32.ComputeChecksum(tmp);
                        break;
                }
            } //Not bosch inv

            if (Complement == 1)
            {
                sum = ~sum;
            }
            if (Complement == 2)
            {
                sum = ~sum + 1;
            }

            switch (Bytes)
            {
                case 1:
                    sum = (sum & 0xFF);
                    break;
                case 2:
                    sum = (sum & 0xFFFF);
                    break;
                case 4:
                    sum = (sum & 0xFFFFFFFF);
                    break;
            }
            if (SwapB)
            {
                sum = SwapBytes(sum,Bytes);
            }
            if (dbg)
                Debug.WriteLine("Result: " + sum.ToString("X"));
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Checksum calc: " + ex.Message);
        }
        return sum;
    }

    public static bool ParseAddress(string Line, PcmFile PCM, out List<Block> Blocks)
    {
        Debug.WriteLine("Segment address line: " + Line);
        Blocks = new List<Block>();

        if (Line == null || Line == "")
        {
            Block B = new Block();
            B.End = PCM.fsize;
            B.Start = 0;
            Blocks.Add(B);
            return true;
        }

        string[] Parts = Line.Split(',');
        int i = 0;

        foreach (string Part in Parts)
        {
            string[] StartEnd = Part.Split('-');
            Block B = new Block();
            int Offset = 0;

            if (StartEnd[0].Contains(">"))
            {
                string[] SO = StartEnd[0].Split('>');
                StartEnd[0] = SO[0];
                uint x;
                if (!HexToUint(SO[1], out x))
                    throw new Exception("Can't decode from HEX: " + SO[1] + " (" + Line + ")");
                Offset = (int)x;
            }
            if (StartEnd[0].Contains("<"))
            {
                string[] SO = StartEnd[0].Split('<');
                StartEnd[0] = SO[0];
                uint x;
                if (!HexToUint(SO[1], out x))
                    throw new Exception("Can't decode from HEX: " + SO[1] + " (" + Line + ")");
                Offset = ~(int)x;
            }


            if (!HexToUint(StartEnd[0].Replace("@", ""), out B.Start))
            {
                throw new Exception("Can't decode from HEX: " + StartEnd[0].Replace("@", "") + " (" + Line + ")");
            }
            if (StartEnd[0].StartsWith("@"))
            {
                uint tmpStart = B.Start;
                B.Start = PCM.readUInt32(tmpStart);
                B.End = PCM.readUInt32(tmpStart + 4);
                tmpStart += 8;
            }
            else
            {
                if (!HexToUint(StartEnd[1].Replace("@", ""), out B.End))
                    throw new Exception("Can't decode from HEX: " + StartEnd[1].Replace("@", "") + " (" + Line + ")");
                if (B.End >= PCM.buf.Length)    //Make 1MB config work with 512kB bin
                    B.End = (uint)PCM.buf.Length - 1;
            }
            if (StartEnd.Length > 1 && StartEnd[1].StartsWith("@"))
            {
                //Read End address from bin at this address
                B.End = PCM.readUInt32(B.End);
            }
            if (StartEnd.Length > 1 && StartEnd[1].EndsWith("@"))
            {
                //Address is relative to end of bin
                uint end;
                if (HexToUint(StartEnd[1].Replace("@", ""), out end))
                    B.End = (uint)PCM.buf.Length - end - 1;
            }
            B.Start += (uint)Offset;
            B.End += (uint)Offset;
            Blocks.Add(B);
            i++;
        }
        foreach (Block B in Blocks)
            Debug.WriteLine("Address block: " + B.Start.ToString("X") + " - " + B.End.ToString("X"));
        return true;
    }


    public static List<string> SelectMultipleFiles(string Title = "Select files", string Filter = "BIN files (*.bin)|*.bin|All files (*.*)|*.*", string defaultFile = "")
    {
        List<string> fileList = new List<string>();

        OpenFileDialog fdlg = new OpenFileDialog();
        if (Filter.Contains("BIN"))
        {
            fdlg.InitialDirectory = UniversalPatcher.Properties.Settings.Default.LastBINfolder;
            Filter = "BIN files (*.bin)|*.bin";
            for (int f = 0; f < fileTypeList.Count; f++)
            {
                string newFilter = "|" + fileTypeList[f].Description + "|" + "*." + fileTypeList[f].Extension;
                Filter += newFilter;
            }
            Filter += "|All files (*.*)|*.*";
        }
        else if (Filter.ToLower().Contains("xdf"))
        {
            fdlg.InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Tunerpro Files", "Bin Definitions");
        }
        else if (defaultFile.Length > 0)
        {

            fdlg.FileName = Path.GetFileName(defaultFile);
            fdlg.InitialDirectory = Path.GetDirectoryName(defaultFile);
        }
        else
        {
            if (Filter.Contains("XML") && !Filter.Contains("PATCH"))
                fdlg.InitialDirectory = UniversalPatcher.Properties.Settings.Default.LastXMLfolder;
            if (Filter.Contains("PATCH") || Filter.Contains("TXT"))
                fdlg.InitialDirectory = UniversalPatcher.Properties.Settings.Default.LastPATCHfolder;
        }

        fdlg.Title = Title;
        fdlg.Filter = Filter;
        fdlg.FilterIndex = 1;
        fdlg.RestoreDirectory = true;
        fdlg.Multiselect = true;
        if (fdlg.ShowDialog() == DialogResult.OK)
        {
            if (Filter.Contains("XML") && !Filter.Contains("PATCH"))
                UniversalPatcher.Properties.Settings.Default.LastXMLfolder = Path.GetDirectoryName(fdlg.FileName);
            else if (Filter.Contains("BIN"))
                UniversalPatcher.Properties.Settings.Default.LastBINfolder = Path.GetDirectoryName(fdlg.FileName);
            else if (Filter.Contains("PATCH"))
                UniversalPatcher.Properties.Settings.Default.LastPATCHfolder = Path.GetDirectoryName(fdlg.FileName);
            UniversalPatcher.Properties.Settings.Default.Save();
            foreach (string fName in fdlg.FileNames)
                fileList.Add(fName);
        }
        return fileList;

    }

    private static string generateFilter()
    {

        string  Filter = "BIN files (*.bin)|*.bin";
        int def = int.MaxValue;
        for (int f = 0; f < fileTypeList.Count; f++)
        {
            if (fileTypeList[f].Default)
                def = f;
        }

        if (def < int.MaxValue)
        {
            Filter = fileTypeList[def].Description + "|" +  fileTypeList[def].Extension;
        }
        for (int f = 0; f < fileTypeList.Count; f++)
        {
            if (f != def)
            {
                string newFilter = "|" + fileTypeList[f].Description + "|" + fileTypeList[f].Extension;
                Filter += newFilter;
            }
        }
        Filter += "|All files (*.*)|*.*";
        return Filter;
    }

    public static string SelectFile(string Title = "Select file", string Filter = "BIN files (*.bin)|*.bin|All files (*.*)|*.*", string defaultFile = "")
    {
        OpenFileDialog fdlg = new OpenFileDialog();
        if (Filter.Contains("BIN"))
        {
            fdlg.InitialDirectory = UniversalPatcher.Properties.Settings.Default.LastBINfolder;
            Filter = generateFilter();
        }
        else if (Filter.ToLower().Contains("xdf"))
        {
            fdlg.InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Tunerpro Files", "Bin Definitions");
        }
        else if (defaultFile.Length > 0)
        {

            fdlg.FileName = Path.GetFileName(defaultFile);
            fdlg.InitialDirectory = Path.GetDirectoryName(defaultFile);
        }
        else
        {
            if (Filter.Contains("XML") && !Filter.Contains("PATCH"))
                fdlg.InitialDirectory = UniversalPatcher.Properties.Settings.Default.LastXMLfolder;
            if (Filter.Contains("PATCH") || Filter.Contains("TXT"))
                fdlg.InitialDirectory = UniversalPatcher.Properties.Settings.Default.LastPATCHfolder;
        }

        fdlg.Title = Title;
        fdlg.Filter = Filter;
        fdlg.FilterIndex = 1;
        fdlg.RestoreDirectory = true;

        if (fdlg.ShowDialog() == DialogResult.OK)
        {
            if (Filter.Contains("XML") && !Filter.Contains("PATCH"))
                UniversalPatcher.Properties.Settings.Default.LastXMLfolder = Path.GetDirectoryName(fdlg.FileName);
            else if (Filter.Contains("BIN"))
                UniversalPatcher.Properties.Settings.Default.LastBINfolder = Path.GetDirectoryName(fdlg.FileName);
            else if (Filter.Contains("PATCH"))
                UniversalPatcher.Properties.Settings.Default.LastPATCHfolder = Path.GetDirectoryName(fdlg.FileName);
            UniversalPatcher.Properties.Settings.Default.Save();
            return fdlg.FileName;
        }
        return "";

    }
    public static string SelectSaveFile(string Filter = "", string defaultFileName = "")
    {
        SaveFileDialog saveFileDialog = new SaveFileDialog();
        //saveFileDialog.Filter = "BIN files (*.bin)|*.bin";
        if (Filter == "" || Filter.Contains("BIN"))
            //saveFileDialog.Filter = "BIN files (*.bin)|*.bin|All files (*.*)|*.*";
            saveFileDialog.Filter = generateFilter();
        else
            saveFileDialog.Filter = Filter;
        saveFileDialog.RestoreDirectory = true;
        saveFileDialog.Title = "Save to file";
        if (defaultFileName.Length > 0)
        {
            saveFileDialog.FileName = Path.GetFileName(defaultFileName);
            string defPath = Path.GetDirectoryName(defaultFileName);
            if (defPath != "")
            {
                saveFileDialog.InitialDirectory = defPath;
            }
        }
        else
        {
            if (Filter.Contains("PATCH"))
                saveFileDialog.InitialDirectory = UniversalPatcher.Properties.Settings.Default.LastPATCHfolder;
            if (Filter.Contains("XML") && !Filter.Contains("PATCH"))
                saveFileDialog.InitialDirectory = UniversalPatcher.Properties.Settings.Default.LastXMLfolder;
            else if (Filter.Contains("BIN"))
                saveFileDialog.InitialDirectory = UniversalPatcher.Properties.Settings.Default.LastBINfolder;
            else if (Filter.Contains("XDF"))
                saveFileDialog.InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Tunerpro Files", "Bin Definitions");
        }

        if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            if (Filter.Contains("XML") && !Filter.Contains("PATCH"))
                UniversalPatcher.Properties.Settings.Default.LastXMLfolder = Path.GetDirectoryName(saveFileDialog.FileName);
            else if (Filter.Contains("BIN"))
                UniversalPatcher.Properties.Settings.Default.LastBINfolder = Path.GetDirectoryName(saveFileDialog.FileName);
            else if (Filter.Contains("PATCH"))
                UniversalPatcher.Properties.Settings.Default.LastPATCHfolder = Path.GetDirectoryName(saveFileDialog.FileName);
            UniversalPatcher.Properties.Settings.Default.Save();
            return saveFileDialog.FileName;
        }
        else
            return "";

    }

    public static string SelectFolder(string Title, string defaultFolder = "")
    {
        string folderPath = "";
        OpenFileDialog folderBrowser = new OpenFileDialog();
        // Set validate names and check file exists to false otherwise windows will
        // not let you select "Folder Selection."
        folderBrowser.ValidateNames = false;
        folderBrowser.CheckFileExists = false;
        folderBrowser.CheckPathExists = true;
        if (defaultFolder.Length > 0)
            folderBrowser.InitialDirectory = defaultFolder;
        else
            folderBrowser.InitialDirectory = UniversalPatcher.Properties.Settings.Default.LastBINfolder;
        // Always default to Folder Selection.
        folderBrowser.Title = Title;
        folderBrowser.FileName = "Folder Selection";
        if (folderBrowser.ShowDialog() == DialogResult.OK)
        {
            folderPath = Path.GetDirectoryName(folderBrowser.FileName);
            UniversalPatcher.Properties.Settings.Default.LastBINfolder = folderPath;
            UniversalPatcher.Properties.Settings.Default.Save();
        }
        return folderPath;
    }

    [DllImport("shell32.dll", SetLastError = true)]
    public static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, uint dwFlags);

    [DllImport("shell32.dll", SetLastError = true)]
    public static extern void SHParseDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr bindingContext, [Out] out IntPtr pidl, uint sfgaoIn, [Out] out uint psfgaoOut);

    public static void OpenFolderAndSelectItem(string folderPath, string file)
    {
        IntPtr nativeFolder;
        uint psfgaoOut;
        SHParseDisplayName(folderPath, IntPtr.Zero, out nativeFolder, 0, out psfgaoOut);

        if (nativeFolder == IntPtr.Zero)
        {
            // Log error, can't find folder
            return;
        }

        IntPtr nativeFile;
        SHParseDisplayName(Path.Combine(folderPath, file), IntPtr.Zero, out nativeFile, 0, out psfgaoOut);

        IntPtr[] fileArray;
        if (nativeFile == IntPtr.Zero)
        {
            // Open the folder without the file selected if we can't find the file
            fileArray = new IntPtr[0];
        }
        else
        {
            fileArray = new IntPtr[] { nativeFile };
        }

        SHOpenFolderAndSelectItems(nativeFolder, (uint)fileArray.Length, fileArray, 0);

        Marshal.FreeCoTaskMem(nativeFolder);
        if (nativeFile != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(nativeFile);
        }
    }

    public static string CheckStockCVN(string PN, string Ver, string SegNr, UInt64 cvnInt, bool AddToList, string XMLFile)
    {
        string retVal = "[n/a]";
        for (int c = 0; c < StockCVN.Count; c++)
        {
            //if (StockCVN[c].XmlFile == Path.GetFileName(XMLFile) && StockCVN[c].PN == PN && StockCVN[c].Ver == Ver && StockCVN[c].SegmentNr == SegNr && StockCVN[c].cvn == cvn)
            if (StockCVN[c].PN == PN && StockCVN[c].Ver == Ver && StockCVN[c].SegmentNr == SegNr)
            {
                if (Path.GetFileName(XMLFile) != StockCVN[c].XmlFile && StockCVN[c].AlternateXML == null)
                {
                    CVN c1 = StockCVN[c];
                    c1.AlternateXML = Path.GetFileName(XMLFile);
                    StockCVN.RemoveAt(c);
                    StockCVN.Insert(c, c1);
                }
                uint stockCvnInt = 0;
                if(HexToUint(StockCVN[c].cvn, out stockCvnInt))
                if (stockCvnInt == cvnInt)
                {
                    retVal = "[stock]";
                    break;
                }
                else
                {
                    retVal = "[modded]";
                    break;
                    //return "[modded]";
                }
            }
        }


        if (retVal == "[n/a]")
        {
            //Check if it's in referencelist
            bool cvnMismatch = false;
            uint refC = 0;
            string refString = "";
            if (referenceCvnList == null) return "";
            for (int r = 0; r < referenceCvnList.Count; r++)
            {
                if (PN == referenceCvnList[r].PN)
                {
                    if (UniversalPatcher.Properties.Settings.Default.RequireValidVerForStock)
                        if (Ver.Contains("?"))
                        {
                            Logger("No valid version");
                            return "[modded/R]";
                        }
                    refString = referenceCvnList[r].CVN;
                    cvnMismatch = true;    //Found from referencelist, match not found YET
                    if (!HexToUint(referenceCvnList[r].CVN, out refC))
                    {
                        LoggerBold("Can't convert from HEX: " + referenceCvnList[r].CVN);
                    }
                    if (refC == cvnInt)
                    {
                        Debug.WriteLine("PN: " + PN + " CVN found from Referencelist: " + referenceCvnList[r].CVN);
                        cvnMismatch = false; //Found from referencelist, no mismatch
						retVal = "[stock]";

                    }
                    ushort refShort;
                    if (!HexToUshort(referenceCvnList[r].CVN, out refShort))
                    {
                        Debug.WriteLine("CheckStockCVN (ushort): Can't convert from HEX: " + referenceCvnList[r].CVN);
                    }
                    if (SwapBytes(refShort,4) == cvnInt)
                    {
                        Debug.WriteLine("PN: " + PN + " byteswapped CVN found from Referencelist: " + referenceCvnList[r].CVN);
                        cvnMismatch = false; //Found from referencelist, no mismatch
						retVal = "[stock]";
                    }
                    else
                    {
                        Debug.WriteLine("Byte swapped CVN doesn't match: " + SwapBytes(refShort,4).ToString("X") + " <> " + cvnInt.ToString("X"));
                    }
                    break;
                }
            }

            if (cvnMismatch) //Found from referencelist, have mismatch
            {
                retVal = "[modded/R]";
                bool isInBadCvnList = false;
                AddToList = false;  //Don't add to CVN list if add to mismatch CVN
                if (BadCvnList == null)
                    BadCvnList = new List<CVN>();
                for (int i = 0; i < BadCvnList.Count; i++)
                {
                    uint badCvnInt = 0;
                    if (HexToUint(BadCvnList[i].cvn, out badCvnInt))
                    {
                        if (BadCvnList[i].PN == PN && badCvnInt == cvnInt)
                        {
                            isInBadCvnList = true;
                            Debug.WriteLine("PN: " + PN + ", CVN: " + cvnInt + " is already in badCvnList");
                            break;
                        }
                    }
                }
                if (!isInBadCvnList)
                {
                    Debug.WriteLine("Adding PN: " + PN + ", CVN: " + cvnInt + " to badCvnList");
                    CVN C1 = new CVN();
                    C1.cvn = cvnInt.ToString("X");
                    C1.PN = PN;
                    C1.SegmentNr = SegNr;
                    C1.Ver = Ver;
                    C1.XmlFile = Path.GetFileName(XMLFile);
                    C1.ReferenceCvn = refString.TrimStart('0');
                    BadCvnList.Add(C1);

                }
            }
        }

        if (AddToList && retVal != "[stock]")
        {
            bool IsinCVNlist = false;
            if (ListCVN == null)
                ListCVN = new List<CVN>();
            for (int c = 0; c < ListCVN.Count; c++)
            {
                //if (ListCVN[c].XmlFile == Path.GetFileName(XMLFile) && ListCVN[c].PN == PN && ListCVN[c].Ver == Ver && ListCVN[c].SegmentNr == SegNr && ListCVN[c].cvn == cvn)
                uint listCvnInt = 0;
                if (HexToUint(ListCVN[c].cvn, out listCvnInt))
                if (ListCVN[c].PN == PN && ListCVN[c].Ver == Ver && ListCVN[c].SegmentNr == SegNr && listCvnInt == cvnInt)
                {
                    Debug.WriteLine("Already in CVN list: " + cvnInt);
                    IsinCVNlist = true;
                    break;
                }
            }
            if (!IsinCVNlist)
            {
                CVN C1 = new CVN();
                C1.cvn = cvnInt.ToString("X");
                C1.PN = PN;
                C1.SegmentNr = SegNr;
                C1.Ver = Ver;
                C1.XmlFile = Path.GetFileName(XMLFile);
                for (int r = 0; r < referenceCvnList.Count; r++)
                {
                    if (referenceCvnList[r].PN == C1.PN)
                    {
                        C1.ReferenceCvn = referenceCvnList[r].CVN.TrimStart('0');
                        break;
                    }
                }
                ListCVN.Add(C1);
            }
        }

        return retVal;
    }

    public static void loadReferenceCvn()
    {
        string FileName = Path.Combine(Application.StartupPath, "XML", "Reference-CVN.xml");
        if (!File.Exists(FileName))
            return;
        referenceCvnList = new List<referenceCvn>();
        System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(List<referenceCvn>));
        System.IO.StreamReader file = new System.IO.StreamReader(FileName);
        referenceCvnList = (List<referenceCvn>)reader.Deserialize(file);
        file.Close();
        foreach (referenceCvn refCvn in referenceCvnList)
        {
            for (int i = 0; i < StockCVN.Count; i++)
            {
                if (StockCVN[i].PN == refCvn.PN)
                {
                    CVN C1 = StockCVN[i];
                    C1.ReferenceCvn = refCvn.CVN;
                    StockCVN.RemoveAt(i);
                    StockCVN.Insert(i, C1);
                }
            }
        }
    }

    public static uint searchBytes(PcmFile PCM, string searchString, uint Start, uint End, ushort stopVal = 0)
    {
        uint addr;
        try
        {
            string[] searchParts = searchString.Trim().Split(' ');
            byte[] bytes = new byte[searchParts.Length];

            for (int b = 0; b < searchParts.Length; b++)
            {
                byte searchval = 0;
                if (searchParts[b] != "*")
                    HexToByte(searchParts[b], out searchval);
                bytes[b] = searchval;
            }

            for (addr = Start; addr < End; addr++)
            {
                bool match = true;
                if (stopVal != 0 && PCM.readUInt16(addr) == stopVal)
                {
                    return uint.MaxValue;
                }
                if ((addr + searchParts.Length) > PCM.fsize)
                    return uint.MaxValue;
                for (uint part = 0; part < searchParts.Length; part++)
                {
                    if (searchParts[part] != "*")
                    {
                        if (PCM.buf[addr + part] != bytes[part])
                        {
                            match = false;
                            break;
                        }
                    }
                }
                if (match)
                {
                    return addr;
                }
            }
        }
        catch (Exception ex)
        {
            var st = new StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(st.FrameCount - 1);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            Debug.WriteLine("Error searchBytes, line " + line + ": " + ex.Message);
        }
        return uint.MaxValue;
    }


    public static SearchedAddress getAddrbySearchString(PcmFile PCM, string searchStr, ref uint startAddr, uint endAddr, bool conditionalOffset = false, bool signedOffset = false)
    {
        SearchedAddress retVal;
        retVal.Addr = uint.MaxValue;
        retVal.Columns = 0;
        retVal.Rows = 0;
        try
        {
            string modStr = searchStr.Replace("r", "");
            modStr = modStr.Replace("k", "");
            modStr = modStr.Replace("x", "");
            modStr = modStr.Replace("y", "");
            modStr = modStr.Replace("@", "*");
            modStr = modStr.Replace("# ", "* "); //# alone at beginning or middle
            if (modStr.EndsWith("#"))
                modStr = modStr.Replace(" #", " *"); //# alone at end
            modStr = modStr.Replace("#", ""); //For example: #21 00 21
            uint addr = searchBytes(PCM, modStr, startAddr, endAddr);
            if (addr == uint.MaxValue)
            {
                //Not found
                startAddr = uint.MaxValue;
                return retVal;
            }

            string[] sParts = searchStr.Trim().Split(' ');
            startAddr = addr + (uint)sParts.Length;

            int[] locations = new int[4];
            int l = 0;
            string addrStr = "*";
            if (searchStr.Contains("@")) addrStr = "@";
            else if (searchStr.Contains("*") || searchStr.Contains("#")) addrStr = "*";
            else
            {
                //Address is AFTER searchstring
                retVal.Addr = PCM.readUInt32(addr + (uint)sParts.Length);
            }
            for (int p = 0; p < sParts.Length; p++)
            {
                if (sParts[p].Contains(addrStr) && l < 4)
                {
                    locations[l] = p;
                    l++;
                }
                if (sParts[p].Contains("r") || sParts[p].Contains("x"))
                {
                    retVal.Rows = (ushort)PCM.buf[(uint)(addr + p)];
                }
                if (sParts[p].Contains("k") || sParts[p].Contains("y"))
                {
                    retVal.Columns = (ushort) PCM.buf[(uint)(addr + p)];
                }
                if (sParts[p].Contains("#"))
                {
                    retVal.Addr = (uint)(addr + p);
                }

            }
            if (retVal.Addr < uint.MaxValue)
            {
                return retVal;
            }

            //We are here, so we must have @ @ @ @  in searchsting
            if (l < 4)
            {
                Logger("Less than 4 @ in searchstring, address need 4 bytes! (" + searchStr + ")");
                retVal.Addr = uint.MaxValue;
            }

            if (PCM.platformConfig.MSB)
                retVal.Addr = (uint)(PCM.buf[addr + locations[0]] << 24 | PCM.buf[addr + locations[1]] << 16 | PCM.buf[addr + locations[2]] << 8 | PCM.buf[addr + locations[3]]);
            else
                retVal.Addr = (uint)(PCM.buf[addr + locations[3]] << 24 | PCM.buf[addr + locations[2]] << 16 | PCM.buf[addr + locations[1]] << 8 | PCM.buf[addr + locations[0]]);
            if (conditionalOffset)
            {
                ushort addrWord = (ushort)(PCM.buf[addr + locations[2]] << 8 | PCM.buf[addr + locations[3]]);
                if (addrWord > 0x5000)
                    retVal.Addr -= 0x10000;
            }
            if (signedOffset)
            {
                ushort addrWord = (ushort)(PCM.buf[addr + locations[2]] << 8 | PCM.buf[addr + locations[3]]);
                if (addrWord > 0x8000)
                    retVal.Addr -= 0x10000;
            }

        }
        catch (Exception ex)
        {
            var st = new StackTrace(ex, true);
            // Get the top stack frame
            var frame = st.GetFrame(st.FrameCount - 1);
            // Get the line number from the stack frame
            var line = frame.GetFileLineNumber();
            Debug.WriteLine ("getAddrbySearchString, line " + line + ": " + ex.Message);
        }
        return retVal;
    }


    public static uint searchWord(PcmFile PCM, ushort sWord, uint Start, uint End, ushort stopVal = 0)
    {
        for (uint addr = Start; addr < End; addr++)
        {
            if (stopVal != 0 && PCM.readUInt16(addr) == stopVal)
            {
                return uint.MaxValue;
            }
            if (PCM.readUInt16(addr) == sWord)
            { 
                return addr;
            }
        }
        return uint.MaxValue;
    }


    public static bool HexToUint64(string Hex, out UInt64 x)
    {
        x = 0;
        if (!UInt64.TryParse(Hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out x))
            return false;
        return true;
    }

    public static bool HexToUint(string Hex, out uint x)
    {
        x = 0;
        if (!UInt32.TryParse(Hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out x))
            return false;
        return true;
    }
    public static bool HexToInt(string Hex, out int x)
    {
        x = 0;
        if (!int.TryParse(Hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out x))
            return false;
        return true;
    }

    public static bool HexToUshort(string Hex, out ushort x)
    {
        x = 0;
        if (!UInt16.TryParse(Hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out x))
            return false;
        return true;
    }
    public static bool HexToByte(string Hex, out byte x)
    {
        x = 0;
        if (!byte.TryParse(Hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out x))
            return false;
        return true;
    }

    public static string ReadTextBlock(byte[] buf, int Address, int Bytes, bool numsLettersOnly = true)
    {
        string result = System.Text.Encoding.ASCII.GetString(buf, (int)Address, Bytes);
        if (numsLettersOnly)
            result = Regex.Replace(result, "[^a-zA-Z0-9]", "?");
        else
            result = Regex.Replace(result, @"[^\u0020-\u007E]", "?");
        return result;
    }

    public static UInt64 readUint64(byte[] buf, uint offset, bool MSB)
    {
        byte[] tmp = new byte[8];
        Array.Copy(buf, offset, tmp, 0, 8);
        if (MSB)
            Array.Reverse(tmp);
        return BitConverter.ToUInt64(tmp,0);
    }

    public static Int64 readInt64(byte[] buf, uint offset, bool MSB)
    {
        byte[] tmp = new byte[8];
        Array.Copy(buf, offset, tmp, 0, 8);
        if (MSB)
            Array.Reverse(tmp);
        return BitConverter.ToInt64(tmp, 0);
    }
    public static Double readFloat64(byte[] buf, uint offset, bool MSB)
    {
        byte[] tmp = new byte[8];
        Array.Copy(buf, offset, tmp, 0, 8);
        if (MSB)
            Array.Reverse(tmp);
        return BitConverter.ToDouble(tmp, 0);
    }
    public static float readFloat32(byte[] buf, uint offset, bool MSB)
    {
        byte[] tmp = new byte[4];
        Array.Copy(buf, offset, tmp, 0, 4);
        if (MSB)
            Array.Reverse(tmp);
        return BitConverter.ToSingle(tmp, 0);
    }

    public static uint readUint32(byte[] buf, uint offset, bool MSB)
    {
        //Shift first byte 24 bits left, second 16bits left...
        //return (uint)((buf[offset] << 24) | (buf[offset + 1] << 16) | (buf[offset + 2] << 8) | buf[offset + 3]);
        byte[] tmp = new byte[4];
        Array.Copy(buf, offset, tmp, 0, 4);
        if (MSB)
            Array.Reverse(tmp);
        return BitConverter.ToUInt32(tmp, 0);
    }

    public static UInt16 readUint16(byte[] buf, uint offset, bool MSB)
    {
        if (MSB)
            return (UInt16)((buf[offset] << 8) | buf[offset + 1]);
        else
            return (UInt16)((buf[offset + 1] << 8) | buf[offset]);
    }

    public static int readInt32(byte[] buf, uint offset, bool MSB)
    {
        //Shift first byte 24 bits left, second 16bits left...
        //return (int)((buf[offset] << 24) | (buf[offset + 1] << 16) | (buf[offset + 2] << 8) | buf[offset + 3]);
        byte[] tmp = new byte[4];
        Array.Copy(buf, offset, tmp, 0, 4);
        if (MSB)
            Array.Reverse(tmp);
        return BitConverter.ToInt32(tmp, 0);
    }

    public static Int16 readInt16(byte[] buf, uint offset, bool MSB)
    {
        if (MSB)
            return (Int16)((buf[offset] << 8) | buf[offset + 1]);
        else
            return (Int16)((buf[offset + 1] << 8) | buf[offset]);
    }
    public static void SaveFloat32(byte[] buf, uint offset, Single data, bool MSB)
    {
        byte[] tmp = new byte[4];
        tmp = BitConverter.GetBytes(data);
        if (MSB)
            Array.Reverse(tmp);
        Array.Copy(tmp, 0, buf, offset, 4);
    }
    public static void SaveFloat64(byte[] buf, uint offset, double data, bool MSB)
    {
        byte[] tmp = new byte[8];
        tmp = BitConverter.GetBytes(data);
        if (MSB)
            Array.Reverse(tmp);
        Array.Copy(tmp, 0, buf, offset, 8);
    }

    public static void SaveUint64(byte[] buf, uint offset, UInt64 data, bool MSB)
    {
        byte[] tmp = new byte[8];
        tmp = BitConverter.GetBytes(data);
        if (MSB)
            Array.Reverse(tmp);
        Array.Copy(tmp,0,buf,offset,8);
    }

    public static void SaveInt64(byte[] buf, uint offset, Int64 data, bool MSB)
    {
        byte[] tmp = new byte[8];
        tmp = BitConverter.GetBytes(data);
        if (MSB)
            Array.Reverse(tmp);
        Array.Copy(tmp, 0, buf, offset, 8);
    }
    public static void SaveUint32(byte[] buf, uint offset, UInt32 data, bool MSB)
    {
        byte[] tmp = new byte[4];
        tmp = BitConverter.GetBytes(data);
        if (MSB)
            Array.Reverse(tmp);
        Array.Copy(tmp, 0, buf, offset, 4);
    }
    public static void SaveInt32(byte[] buf, uint offset, Int32 data, bool MSB)
    {
        byte[] tmp = new byte[4];
        tmp = BitConverter.GetBytes(data);
        if (MSB)
            Array.Reverse(tmp);
        Array.Copy(tmp, 0, buf, offset, 4);
    }

    public static void Save3Bytes(byte[] buf, uint offset, UInt32 data, bool MSB)
    {
        if (MSB)
        {
            buf[offset] = (byte)(data & 0xff);
            buf[offset + 1] = (byte)((data >> 8) & 0xff);
            buf[offset + 2] = (byte)((data >> 16) & 0xff);
        }
        else
        {
            buf[offset + 2] = (byte)(data & 0xff);
            buf[offset + 1] = (byte)((data >> 8) & 0xff);
            buf[offset] = (byte)((data >> 16) & 0xff);
        }

    }


    public static void SaveUshort(byte[] buf, uint offset, ushort data, bool MSB)
    {
        byte[] tmp = new byte[2];
        tmp = BitConverter.GetBytes(data);
        if (MSB)
        Array.Reverse(tmp);
        Array.Copy(tmp, 0, buf, offset, 2);
    }
    public static void SaveShort(byte[] buf, uint offset, short data, bool MSB)
    {
        byte[] tmp = new byte[2];
        tmp = BitConverter.GetBytes(data);
        if (MSB)
            Array.Reverse(tmp);
        Array.Copy(tmp, 0, buf, offset, 2);
    }

/*    public static ushort SwapBytes(ushort x)
    {
        return (ushort)((ushort)((x & 0xff) << 8) | ((x >> 8) & 0xff));
    }

    public static uint SwapBytes(uint x)
    {
        return ((x & 0x000000ff) << 24) +
               ((x & 0x0000ff00) << 8) +
               ((x & 0x00ff0000) >> 8) +
               ((x & 0xff000000) >> 24);
    }
*/
    public static UInt64 SwapBytes(UInt64 data, int bytes)
    {
        byte[] tmp = new byte[8];
        tmp = BitConverter.GetBytes(data);
        byte[] tmp2 = new byte[bytes];
        Array.Copy(tmp, 0, tmp2, 0, bytes);
        Array.Reverse(tmp2);
        tmp = BitConverter.GetBytes((UInt64)0);
        Array.Copy(tmp2, tmp, bytes);
        return BitConverter.ToUInt64(tmp, 0);
    }

    public static void UseComboBoxForEnums(DataGridView g)
    {
        try
        {
            g.Columns.Cast<DataGridViewColumn>()
             .Where(x => x.ValueType.IsEnum && x.GetType() != typeof(DataGridViewComboBoxColumn))
             .ToList().ForEach(x =>
             {
                 var index = x.Index;
                 g.Columns.RemoveAt(index);
                 var c = new DataGridViewComboBoxColumn();
                 c.ValueType = x.ValueType;
                 c.ValueMember = "Value";
                 c.DisplayMember = "Name";
                 c.DataPropertyName = x.DataPropertyName;
                 c.HeaderText = x.HeaderText;
                 c.Name = x.Name;
                 if (x.ValueType.IsEnum)
                 {
                     c.DataSource = Enum.GetValues(x.ValueType).Cast<object>().Select(v => new
                     {
                         Value = (int)v,
                         Name = Enum.GetName(x.ValueType, v) /* or any other logic to get text */
                     }).ToList();
                 }

                 g.Columns.Insert(index, c);
             });
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }


    public static int findTableDataId(TableData refTd, List<TableData> tdList)
    {
        int pos1 = refTd.TableName.IndexOf("*");
        if (pos1 < 0)
            pos1 = refTd.TableName.Length;

        string refTableName = refTd.TableName.ToLower().Substring(0, pos1).Replace(" ", "_");
        for (int t = 0; t < tdList.Count; t++)
        {
            int pos2 = tdList[t].TableName.IndexOf("*");
            if (pos2 < 0)
                pos2 = tdList[t].TableName.Length;
            //if (pcm1.tableDatas[t].TableName.ToLower().Substring(0, pos2) == refTd.TableName.ToLower().Substring(0, pos1) && pcm1.tableDatas[t].Category.ToLower() == refTd.Category.ToLower())
            if (tdList[t].TableName.ToLower().Substring(0, pos2).Replace(" ","_") == refTableName)
            {
                return t;
            }
        }
        //Not found (exact match) maybe close enough?
        int required = UniversalPatcher.Properties.Settings.Default.TunerMinTableEquivalency;
        if (required == 100)
            return -1;  //already searched for 100% match
        for (int t = 0; t < tdList.Count; t++)
        {
            int pos2 = tdList[t].TableName.IndexOf("*");
            if (pos2 < 0)
                pos2 = tdList[t].TableName.Length;
            double percentage = ComputeSimilarity.CalculateSimilarity(tdList[t].TableName.ToLower().Substring(0, pos2).Replace(" ", "_"), refTableName);
            if ((int)(percentage * 100) >= required )
            {
                Debug.WriteLine(refTd.TableName + " <=> " + tdList[t].TableName + "; Equivalency: " + (percentage * 100).ToString() + "%");
                return t;
            }
        }

        return -1;
    }

    public static void Logger(string LogText, Boolean NewLine = true)
    {
        try
        {
            frmpatcher.Logger(LogText, NewLine);
            for (int l = LogReceivers.Count - 1; l >= 0;  l--)
            {
                if (LogReceivers[l].IsDisposed)
                    LogReceivers.RemoveAt(l);
            }
            for (int l=0; l< LogReceivers.Count; l++)
            {
                LogReceivers[l].AppendText(LogText);
                if (NewLine)
                    LogReceivers[l].AppendText(Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.InnerException);
        }
    }
    public static void LoggerBold(string LogText, Boolean NewLine = true)
    {
        try
        {
            frmpatcher.LoggerBold(LogText, NewLine);
            for (int l = LogReceivers.Count - 1; l >= 0; l--)
            {
                if (LogReceivers[l].IsDisposed)
                    LogReceivers.RemoveAt(l);
            }
            for (int l = 0; l < LogReceivers.Count; l++)
            {
                LogReceivers[l].SelectionFont = new Font(LogReceivers[l].Font, FontStyle.Bold);
                LogReceivers[l].AppendText(LogText);
                LogReceivers[l].SelectionFont = new Font(LogReceivers[l].Font, FontStyle.Regular);
                if (NewLine)
                    LogReceivers[l].AppendText(Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.InnerException);
        }
    }

    private static string GetNextBase26(string a)
    {
        return Base26Sequence().SkipWhile(x => x != a).Skip(1).First();
    }

    private static IEnumerable<string> Base26Sequence()
    {
        long i = 0L;
        while (true)
            yield return Base26Encode(i++);
    }

    private static char[] base26Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
    public static string Base26Encode(Int64 value)
    {
        string returnValue = null;
        do
        {
            returnValue = base26Chars[value % 26] + returnValue;
            value /= 26;
        } while (value-- != 0);
        return returnValue;
    }

    public static string GetShortcutTarget(string shortcutFilename)
    {
        try
        {
            string pathOnly = System.IO.Path.GetDirectoryName(shortcutFilename);
            string filenameOnly = System.IO.Path.GetFileName(shortcutFilename);

            Shell32.Shell shell = new Shell32.Shell();
            Shell32.Folder folder = shell.NameSpace(pathOnly);
            Shell32.FolderItem folderItem = folder.ParseName(filenameOnly);
            if (folderItem != null)
            {
                Shell32.ShellLinkObject link = (Shell32.ShellLinkObject)folderItem.GetLink;
                return link.Path;
            }
            return ""; // not found
        }
        catch
        {
            return "";
        }
    }

 
    public static void CreateShortcut(string DestinationFolder,string scName, string args)
    {
        object shDesktop = (object)"Desktop";
    
        IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
        string shortcutAddress = (string)shell.SpecialFolders.Item(ref shDesktop);
        if (DestinationFolder.Length > 0)
            shortcutAddress = DestinationFolder;
        shortcutAddress = Path.Combine(shortcutAddress, scName + ".lnk");
        IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutAddress);
        shortcut.Description = "UniversalPatcher";
        shortcut.Arguments = args;
        shortcut.TargetPath = Application.ExecutablePath;
        shortcut.Save();
    }

    public static List<XmlPatch> loadPatchFile(string fileName)
    {
        System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(List<XmlPatch>));
        System.IO.StreamReader file = new System.IO.StreamReader(fileName);
        List<XmlPatch> pList = (List<XmlPatch>)reader.Deserialize(file);
        file.Close();
        return pList;
    }

    public static List<TableData> filterTdList(IEnumerable<TableData> results, string filterTxt, string filterBy, bool caseSens)
    {
        TableData tdTmp = new TableData();
        try
        {
            if (caseSens)
                results = results.Where(t => typeof(TableData).GetProperty(filterBy).GetValue(t, null).ToString().Contains(filterTxt.Trim()));
            else
                results = results.Where(t => typeof(TableData).GetProperty(filterBy).GetValue(t, null).ToString().ToLower().Contains(filterTxt.ToLower().Trim()));
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            List<TableData> emptyList = new List<TableData>();
            return emptyList;
        }
        return results.ToList();
    }
}