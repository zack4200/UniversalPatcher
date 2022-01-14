﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Upatcher;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.Remoting.Messaging;
using System.Management.Instrumentation;

namespace UniversalPatcher
{
    public class DtcSearchConfig
    {
        public DtcSearchConfig()
        {
            MilTable = "opcode";
            CodeOffset = 1;
            CodeSteps = 4;
            StatusOffset = 1;
            MilOffset = 1;
            StatusSteps = 1;
            MilSteps = 1;
            ConditionalOffset = "";
            Linear = true;
            CodesPerStatus = 1;
        }
        public string XMLFile { get; set; }
        public string CodeSearch { get; set; }
        public string StatusSearch { get; set; }
        public string MilSearch { get; set; }
        public string MilTable { get; set; }
        public Int64 CodeOffset { get; set; }
        public int CodeSteps { get; set; }
        public Int64 StatusOffset { get; set; }
        public Int64 MilOffset { get; set; }
        public int StatusSteps { get; set; }
        public int MilSteps { get; set; }
        public string ConditionalOffset { get; set; }
        public string Values { get; set; }
        public bool Linear { get; set; }
        public int CodesPerStatus { get; set; }

        public DtcSearchConfig ShallowCopy()
        {
            return (DtcSearchConfig)this.MemberwiseClone();
        }
    }

    public class DtcSearch
    {
        public DtcSearch()
        {

        }

        public string DecodeDTC(string code)
        {
            if (code.StartsWith("5"))
            {
                return "U0" + code.Substring(1);
            }
            else if (code.StartsWith("C"))
            {
                return "C0" + code.Substring(1);
            }
            else if (code.StartsWith("D"))
            {
                return "U1" + code.Substring(1);
            }
            else if (code.StartsWith("E"))
            {
                return "U2" + code.Substring(1);
            }
            else
            {
                return "P" + code;
            }
        }

        public string GetDtcDescription(string dtcCode)
        {
            string retVal = "";
            for (int o = 0; o < OBD2Codes.Count; o++)
            {
                if (dtcCode == OBD2Codes[o].Code)
                {
                    retVal = OBD2Codes[o].Description;
                    break;
                }
            }
            return retVal;
        }

        public List<DtcCode> SearchDtc(PcmFile PCM, bool primary)
        {
            List<DtcCode> retVal = new List<DtcCode>();
            try
            {
                LoadOBD2Codes();
                //Search DTC codes:
                uint codeAddr = uint.MaxValue;
                string searchStr;
                int configIndex = 0;
                uint startAddr = 0;
                PCM.dtcCombined = false;
                bool condOffsetCode = false;
                bool condOffsetStatus = false;
                bool condOffsetMil = false;
                bool signedOffsetCode = false;
                bool signedOffsetStatus = false;
                bool signedOffsetMil = false;
                uint statusAddr = uint.MaxValue;
                uint milAddr = uint.MaxValue;
                bool linear = true;
                int codePerStatus = 1;

                string cnfFile = PCM.configFile;
                if (!primary)
                    cnfFile += "-dtc2";

                for (configIndex = 0; configIndex < dtcSearchConfigs.Count; configIndex++)
                {
                    linear = dtcSearchConfigs[configIndex].Linear;
                    if (dtcSearchConfigs[configIndex].CodesPerStatus > 0)
                        codePerStatus = dtcSearchConfigs[configIndex].CodesPerStatus;

                    if (dtcSearchConfigs[configIndex].XMLFile != null && cnfFile == dtcSearchConfigs[configIndex].XMLFile.ToLower())
                    {
                        searchStr = dtcSearchConfigs[configIndex].CodeSearch;
                        startAddr = 0;
                        condOffsetCode = false;
                        string[] condParts = dtcSearchConfigs[configIndex].ConditionalOffset.Trim().Split(',');
                        foreach (string conPart in condParts)
                        {
                            switch(conPart.Trim())
                            {
                                case "code":
                                    condOffsetCode = true;
                                    break;
                                case "status":
                                    condOffsetStatus = true;
                                    break;
                                case "mil":
                                    condOffsetMil = true;
                                    break;
                                case "signed:code":
                                    signedOffsetCode = true;
                                    break;
                                case "signed:status":
                                    signedOffsetStatus = true;
                                    break;
                                case "signed:mil":
                                    signedOffsetMil = true;
                                    break;
                            }
                        }

                        codeAddr = GetAddrbySearchString(PCM, searchStr,ref startAddr,PCM.fsize, condOffsetCode, signedOffsetCode).Addr;
                        //Check if we found status table, too:
                        startAddr = 0;
                        statusAddr = GetAddrbySearchString(PCM, dtcSearchConfigs[configIndex].StatusSearch, ref startAddr,PCM.fsize, condOffsetStatus, signedOffsetStatus).Addr;
                        if (codeAddr < uint.MaxValue) //If found
                            codeAddr = (uint)(codeAddr + dtcSearchConfigs[configIndex].CodeOffset);
                        if (statusAddr < uint.MaxValue)
                            statusAddr = (uint)(statusAddr + dtcSearchConfigs[configIndex].StatusOffset);
                        if (codeAddr < PCM.fsize && statusAddr < PCM.fsize)
                        {
                            Debug.WriteLine("Code search string: " + searchStr);
                            Debug.WriteLine("DTC code table address: " + codeAddr.ToString("X"));
                            if (condOffsetStatus)
                            {
                                uint a = codeAddr;
                                int prevCode = PCM.ReadUInt16(a);
                                for (int x = 0; x < 30; x++)
                                {
                                    //Check if codes (30 first) are increasing
                                    a += (uint)dtcSearchConfigs[configIndex].CodeSteps;
                                    int nextCode = PCM.ReadUInt16(a);
                                    if (nextCode <= prevCode)
                                    {
                                        //Not increasing, use offset
                                        codeAddr += 0x10000;
                                        break;
                                    }
                                    prevCode = PCM.ReadUInt16(a);
                                }
                            }
                            Debug.WriteLine("DTC status table address: " + statusAddr.ToString("X"));
                            break;
                        }
                        else
                        {
                            codeAddr = 0;
                        }

                    }
                }

                if (codeAddr == uint.MaxValue || codeAddr == 0)
                {
                    if (PCM.configFile == "e38" || PCM.configFile == "e67")
                    {
                        PCM.dtcCombined = true;
                        string vals = "Enum: 0:MIL and reporting off,1:Type A/no MIL,2:Type B/no MIL,3:Type C/no MIL,4:Not reported/MIL,5:Type A/MIL,6:Type B/MIL,7:Type C/MIL";
                        PCM.dtcValues = ParseDtcValues(vals.ToLower().Replace("enum: ", ""));
                        return SearchDtcE38(PCM);
                    }
                    else if (primary)
                    {
                        LoggerBold("DTC search: can't find DTC code table");
                        return null;
                    }
                    else
                    {
                        return null;
                    }
                }

                //Read codes:
                bool dCodes = false;
                for (uint addr = codeAddr; addr < PCM.fsize; addr += (uint)dtcSearchConfigs[configIndex].CodeSteps)
                {
                    DtcCode dtc = new DtcCode();
                    dtc.codeAddrInt = addr;
                    dtc.Values = dtcSearchConfigs[configIndex].Values;
                    dtc.CodeAddr = addr.ToString("X8");
                    dtc.codeInt = PCM.ReadUInt16(addr);

                    string codeTmp = dtc.codeInt.ToString("X");
                    if (dtc.codeInt < 10 && retVal.Count > 10 && linear)
                            break;
                    if (dCodes && linear)
                    {
                        //There should be only D or E (U) codes after first D code
                        if (!codeTmp.StartsWith("D") && !codeTmp.StartsWith("E"))
                            break;
                    }
                    codeTmp = dtc.codeInt.ToString("X4");
                    if ( codeTmp.StartsWith("6") || codeTmp.StartsWith("7") || codeTmp.StartsWith("8")
                        || codeTmp.StartsWith("9")  || codeTmp.StartsWith("B") || codeTmp.StartsWith("F"))
                    {
                        Debug.WriteLine("DTC Code out of range: " + codeTmp);
                        break;
                    }
                    dtc.Code = DecodeDTC(codeTmp);
                    if (codeTmp.StartsWith("D")) 
                        dCodes = true;
                    //Find description for code:
                    dtc.Description = GetDtcDescription(dtc.Code);
                    retVal.Add(dtc);
                }

                //PCM.dtcCodes = PCM.dtcCodes.OrderBy(x => x.codeInt).ToList();
                List<uint> milAddrList = new List<uint>();

                if (dtcSearchConfigs[configIndex].MilTable == "afterstatus")
                {
                    milAddr = (uint)(statusAddr + retVal.Count + (uint)dtcSearchConfigs[configIndex].MilOffset);
                    if (PCM.configFile == "p01-p59" && PCM.buf[milAddr - 1] == 0xFF) milAddr++; //P59 hack: If there is FF before first byte, skip first byte 
                    milAddrList.Add(milAddr);
                }
                else if (dtcSearchConfigs[configIndex].MilTable == "combined")
                {
                    //Do nothing for now
                    milAddr = 0;
                    milAddrList.Add(milAddr);
                    PCM.dtcCombined = true;
                }
                else
                {
                    //Search MIL table
                    startAddr = 0;
                    for (int i = 0; i < 30; i++)
                    {
                        milAddr = GetAddrbySearchString(PCM, dtcSearchConfigs[configIndex].MilSearch, ref startAddr, PCM.fsize, condOffsetMil, signedOffsetMil).Addr;
                        if (milAddr < uint.MaxValue)
                        {

                            milAddr +=  (uint)dtcSearchConfigs[configIndex].MilOffset;
                            if (milAddr < PCM.fsize) //Hit
                            {
                                milAddrList.Add(milAddr);
                                //break;
                            }
                        }
                        else
                        {
                            //Not found
                            break;
                        }
                    }
                }


                if (milAddrList.Count > 1)
                {
                    //IF have multiple hits for search, use table which starts after FF, ends before FF
                    for (int m = 0; m < milAddrList.Count; m++)
                    {
                        Debug.WriteLine("MIL Start: " + (milAddrList[m] - 1).ToString("X"));
                        Debug.WriteLine("MIL End: " + (milAddrList[m] + retVal.Count).ToString("X"));
                        if (PCM.buf[milAddrList[m] - 2] == 0xFF && PCM.buf[milAddrList[m] + retVal.Count] == 0xFF)
                        {
                            milAddr = milAddrList[m];
                            break;
                        }
                    }
                    if (milAddr == uint.MaxValue)
                    {
                        //We didn't found it, use first from list
                        milAddr = milAddrList[0];
                    }
                }
                else milAddr = milAddrList[0];
                Debug.WriteLine("MIL: " + milAddr.ToString("X"));
                if (milAddr >= PCM.fsize)
                {
                   LoggerBold( "DTC search: MIL table address out of address range:" + milAddr.ToString("X8"));
                    return null;
                }

                //Read DTC status bytes:
                string values;
                if (dtcSearchConfigs[configIndex].Values != null && dtcSearchConfigs[configIndex].Values.Length > 0)
                {
                    values = dtcSearchConfigs[configIndex].Values;
                }
                else if (PCM.dtcCombined)
                {
                    values = "Enum: 0:MIL and reporting off,1:Type A/no MIL,2:Type B/no MIL,3:Type C/no MIL,4:Not reported/MIL,5:Type A/MIL,6:Type B/MIL,7:Type C/MIL";
                }
                else
                {
                    values = "Enum: 0:1 Trip/immediately,1:2 Trips,2:Store only,3:Disabled"; ;
                }
                PCM.dtcValues = ParseDtcValues(values);
                int dtcNr = 0;
                uint addr2 = statusAddr;
                uint addr3 = milAddr;
                uint codePerStatusaddr = statusAddr;
                uint codePerStatusCounter = 0;
                for (; dtcNr < retVal.Count; addr2+= (uint)dtcSearchConfigs[configIndex].StatusSteps, addr3+= (uint)dtcSearchConfigs[configIndex].MilSteps)
                {

                    /*if (PCM.buf[addr2] > 7)
                    {
                        if (linear)
                            break;
                        else
                            continue;
                    }
                    if (!PCM.dtcCombined && PCM.buf[addr2] > 3) //DTC = 0-3
                    {
                        if (linear)
                            break;
                        else
                            continue;
                    }*/
                    DtcCode dtc = retVal[dtcNr];

                    byte statusByte;
                    dtc.statusAddrInt = codePerStatusaddr;
                    statusByte = PCM.buf[codePerStatusaddr];
                    dtc.Status = statusByte;
                    codePerStatusCounter++;
                    if (codePerStatusCounter >= codePerStatus)
                    {
                        codePerStatusCounter = 0;
                        codePerStatusaddr++;
                    }
                    if (PCM.dtcValues.ContainsKey(dtc.Status))
                    {
                        dtc.StatusTxt = PCM.dtcValues[dtc.Status].ToString();
                    }
                    else
                    {
                        Debug.WriteLine("DTC Status out of range: " + dtc.Status);
                        break;
                    }
                    if (PCM.dtcCombined)
                    {
                        if (statusByte > 4)
                            dtc.MilStatus = 1;
                        else
                            dtc.MilStatus = 0;
                    }
                    else
                    {
                        //Read MIL bytes:
                        dtc.milAddrInt = addr3;
                        dtc.MilAddr = addr3.ToString("X8");
                        dtc.MilStatus = PCM.buf[addr3];
                    }
                    retVal.RemoveAt(dtcNr);
                    retVal.Insert(dtcNr, dtc);
                    dtcNr++;
                }
                //Remove codes with invalid status:
                if (dtcNr<retVal.Count - 1)
                {
                    for (int x = retVal.Count - 1; x >= dtcNr; x--)
                        retVal.RemoveAt(x);
                }

            }
            catch (Exception ex)
            {
                var st = new StackTrace(ex, true);
                // Get the top stack frame
                var frame = st.GetFrame(st.FrameCount - 1);
                // Get the line number from the stack frame
                var line = frame.GetFileLineNumber();
                LoggerBold( "DTC search, line " + line + ": " + ex.Message);
            }
            return retVal;
        }
        //Search GM e38/e67 DTC codes
        public List<DtcCode> SearchDtcE38(PcmFile PCM)
        {
            List<DtcCode> retVal = new List<DtcCode>();
            try
            {
                if (PCM.OSSegment == -1)
                {
                    LoggerBold("DTC search: No OS segment??");
                    return null;
                }
                if (PCM.DiagSegment == -1)
                {
                    LoggerBold( "DTC search: No Diagnostic segment??");
                    return null;
                }

                //Get codes from OS segment:
                string searchStr = "3D 80 * * 39 8C * * 7D 7B DA 14 7C 8C 5A 2E 80 BD";
                uint opCodeAddr = uint.MaxValue;
                uint tableStart = 0;
                for (int b = 0; b < PCM.segmentAddressDatas[PCM.OSSegment].SegmentBlocks.Count; b++)
                {
                    opCodeAddr = SearchBytes(PCM, searchStr, PCM.segmentAddressDatas[PCM.OSSegment].SegmentBlocks[b].Start, PCM.segmentAddressDatas[PCM.OSSegment].SegmentBlocks[b].End);
                    if (opCodeAddr < uint.MaxValue)
                    {
                        ushort highBytes = PCM.ReadUInt16((uint)(opCodeAddr + 2));
                        ushort lowBytes = PCM.ReadUInt16((uint)(opCodeAddr + 6));
                        tableStart = (uint)(highBytes << 16 | lowBytes);
                        ushort tmp = (ushort)(opCodeAddr & 0xffff);
                        if (tmp > 0x5000) tableStart -= 0x10000; //Some kind of address offset 
                        bool dCodes = false;
                        for (uint addr = tableStart; addr < PCM.fsize; addr += 2)
                        {
                            DtcCode dtc = new DtcCode();
                            dtc.codeAddrInt = addr;
                            dtc.CodeAddr = addr.ToString("X8");
                            dtc.codeInt = PCM.ReadUInt16(addr);

                            string codeTmp = dtc.codeInt.ToString("X4");
                            if (dCodes && !codeTmp.StartsWith("D"))
                            {
                                break;
                            }
                            if (codeTmp.StartsWith("D"))
                            {
                                dCodes = true;
                            }
                            dtc.Code = DecodeDTC(codeTmp);
                            dtc.Description = GetDtcDescription(dtc.Code);
                            retVal.Add(dtc);
                        }
                        break;
                    }
                }

                int dtcCount = retVal.Count;

                //Search by opcode:
                opCodeAddr = SearchBytes(PCM, "3C A0 * * 38 A5 * * 7D 85 50", 0, PCM.fsize);
                if (opCodeAddr < uint.MaxValue)
                {
                    ushort highBytes = PCM.ReadUInt16((uint)(opCodeAddr + 2));
                    ushort lowBytes = PCM.ReadUInt16((uint)(opCodeAddr + 6));
                    tableStart = (uint)(highBytes << 16 | lowBytes);
                }

                if (tableStart > 0)
                {
                    int dtcNr = 0;
                    for (uint addr2 = tableStart; addr2 < tableStart + dtcCount; addr2++)
                    {
                        if (PCM.buf[addr2] == 0xFF)
                        {
                            return retVal;
                        }
                        DtcCode dtc = retVal[dtcNr];
                        dtc.statusAddrInt = addr2;
                        //dtc.StatusAddr = addr2.ToString("X8");
                        //dtc.Status = PCM.buf[addr2];
                        byte statusByte = PCM.buf[addr2];
                        if (statusByte > 3)
                            dtc.MilStatus = 1;
                        else
                            dtc.MilStatus = 0;
                        dtc.Status = statusByte;
                        dtc.StatusTxt = PCM.dtcValues[dtc.Status].ToString();
                        retVal.RemoveAt(dtcNr);
                        retVal.Insert(dtcNr, dtc);
                        dtcNr++;
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
                LoggerBold( "DTC search, line " + line + ": " + ex.Message);
                return null;
            }
            return retVal;
        }

    }

}
