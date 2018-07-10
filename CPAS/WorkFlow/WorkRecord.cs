﻿using CPAS.Classes;
using CPAS.Config;
using CPAS.Config.SoftwareManager;
using CPAS.Instrument;
using CPAS.Models;
using GalaSoft.MvvmLight.Messaging;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CPAS.WorkFlow
{
    public class WorkRecord : WorkFlowBase
    {
        private PowerMeter Pw1000USB_1 = null;
        private PowerMeter Pw1000USB_2 = null;
        private LDS lds1 = null;
        private LDS lds2 = null;
        private Keyence_SR1000 BarcodeScanner1 = null;
        private Keyence_SR1000 BarcodeScanner2 = null;

        QSerisePlc PLC = null;
        CancellationTokenSource ctsMonitorPower = null;
        private string FILE_FAKE_BARCODE_FILE = FileHelper.GetCurFilePathString() + "UserData\\Barcode.xls";
        private DataTable Fake_Barcode_Dt = new DataTable();

        //UnLock cmd
        private int nCmdUnLock1 = -1;
        private int nCmdUnLock2 = -1;

        //scan barcode cmd
        private int nCmdScanbarcode1 = -1;
        private int nCmdScanbarcode2 = -1;
        private string strBarcode1 = "";
        private string strBarcode2 = "";

        //Adjust laser cmd
        private int nCmdAdjustLaser1 = -1;
        private int nCmdAdjustLaser2 = -1;
        bool? bAdjustLaser1Ok = null;
        bool? bAdjustLaser2Ok = null;

        private enum STEP : int
        {
            INIT = 1,

            #region Station 1
            Check_Enable_UnLock,
            Wait_UnLock_Cmd,
            Write_Unlock_Result,

            Check_Enable_ScanBarcode,
            Wait_Scan_Barcode_Cmd,
            Write_Barcode_To_Register,
            Write_Scan_Result,

            Check_Enable_Adjust_Laser_Power,
            Wait_Adjust_Laser_Power_Cmd,
            Adjust_Power,
            Write_Adjust_Laser_Power_Result,
            #endregion

            EMG,
            EXIT,
            DO_NOTHING,
        }

        protected override  bool UserInit()
        {
          
            bool bRet = false;
            
            #region >>>>读取模块配置信息，初始化工序Enable信息
            if (GetPresInfomation())
                ShowInfo("加载参数成功");
            else
                ShowInfo("加载参数失败,请确认是否选择参数配方");
            #endregion

            #region >>>>初始化仪表信息
            Pw1000USB_1 = InstrumentMgr.Instance.FindInstrumentByName("PowerMeter[0]") as PowerMeter;
            Pw1000USB_2 = InstrumentMgr.Instance.FindInstrumentByName("PowerMeter[1]") as PowerMeter;
            lds1 = InstrumentMgr.Instance.FindInstrumentByName("LDS[0]") as LDS;
            lds2 = InstrumentMgr.Instance.FindInstrumentByName("LDS[1]") as LDS;
            BarcodeScanner1 = InstrumentMgr.Instance.FindInstrumentByName("SR1000[0]") as Keyence_SR1000;
            BarcodeScanner2 = InstrumentMgr.Instance.FindInstrumentByName("SR1000[1]") as Keyence_SR1000;
            PLC = InstrumentMgr.Instance.FindInstrumentByName("PLC") as QSerisePlc;
            
#if TEST
            //string strTest = "ABCDEFGHIJKPRicky124567IUTVNghj";
            //PLC.WriteString("R100", strTest);
            //string str = PLC.ReadString("R100", strTest.Length);
#endif

            #endregion

            LogExcel Fake_Barcode_Excel = new LogExcel(FILE_FAKE_BARCODE_FILE);
            Fake_Barcode_Excel.ExcelToDataTable(ref Fake_Barcode_Dt, "Sheet1");

            bRet =  Pw1000USB_1 != null &&
                    Pw1000USB_2 != null &&
                    lds1 != null &&
                    lds2 != null &&
                    BarcodeScanner1 != null &&
                    BarcodeScanner2 != null &&
                    PLC != null;
            if (!bRet)
                ShowInfo("初始化失败");


            return true;
            return bRet;

        }
        public WorkRecord(WorkFlowConfig cfg) : base(cfg)
        {
            #region >>>>

            #endregion
        }

        protected override int WorkFlow()
        {

            ClearAllStep();
            PushStep(STEP.INIT);
            while (!cts.IsCancellationRequested)
            {
                nStep = PeekStep();
                switch (nStep)
                {
                    case STEP.INIT:
                        PopAndPushStep(STEP.Check_Enable_UnLock);
                        ShowInfo();
                        Thread.Sleep(100);
                        break;

                    #region >>>>解锁
                    case STEP.Check_Enable_UnLock:
                        if (Prescription.UnLock)
                        {

                            PopAndPushStep(STEP.Wait_UnLock_Cmd);
                        }
                        else
                        {
                            PopAndPushStep(STEP.Check_Enable_ScanBarcode);//否则直接跳到等待扫码工序
                        }
                        break;


                    case STEP.Wait_UnLock_Cmd:
                        Thread.Sleep(200);
                        nCmdUnLock1 = PLC.ReadInt("R13");
                        nCmdUnLock2 = PLC.ReadInt("R14");
                        if (1 == nCmdUnLock1 || 1 == nCmdUnLock2)
                        {
                            Thread.Sleep(200);
                            nCmdUnLock1 = PLC.ReadInt("R13");  //清空命令
                            nCmdUnLock2 = PLC.ReadInt("R14");
                            if (1 == nCmdUnLock1 || 1 == nCmdUnLock2)
                            {
                                if (1 == nCmdUnLock1)
                                {
                                    //解锁1
                                    SendUnlockReq(nCmdUnLock1);
                                }
                                if (1 == nCmdUnLock2)
                                {
                                    //解锁2
                                    SendUnlockReq(nCmdUnLock2);
                                }
                                PopAndPushStep(STEP.Write_Unlock_Result);
                            }
                        }
                        break;
                    case STEP.Write_Unlock_Result:     
                        if ((1 == nCmdUnLock1 || 1 == nCmdUnLock2))
                        {
                            string[] strRet = GetUnlockResult();
                            if (strRet != null)
                            {
                                if (1 == nCmdUnLock1)
                                    PLC.WriteInt("R15", strRet[0].ToLower()=="ok" ? 2 : 1);        //将解锁结果写入寄存器
                                if (1 == nCmdUnLock2)
                                    PLC.WriteInt("R16", strRet[1].ToLower() == "ok" ? 2 : 1);
                                if (true || strRet!=null)  //等待结果过来                 dddd
                                    PopAndPushStep(STEP.Check_Enable_ScanBarcode);
                            }
                        }
                        else
                        {
                            PopAndPushStep(STEP.Check_Enable_ScanBarcode);
                        }
                        break;
                    #endregion


                    #region >>>>扫码
                    case STEP.Check_Enable_ScanBarcode:
                        if (Prescription.ReadBarcode)
                        {
                            PopAndPushStep(STEP.Wait_Scan_Barcode_Cmd);
                        }
                        else
                        {
                            PopAndPushStep(STEP.Check_Enable_Adjust_Laser_Power);//否则直接跳到调激光工序
                        }
                        break;
                    case STEP.Wait_Scan_Barcode_Cmd:
                        Thread.Sleep(200);
                        nCmdScanbarcode1 = PLC.ReadInt("R18");
                        nCmdScanbarcode2 = PLC.ReadInt("R42");
                        if (1 == nCmdScanbarcode1 || 1 == nCmdScanbarcode2)
                        {
                            Thread.Sleep(200);
                            nCmdScanbarcode1 = PLC.ReadInt("R18");
                            nCmdScanbarcode2 = PLC.ReadInt("R42");
                            if (1 == nCmdScanbarcode1 || 1 == nCmdScanbarcode2)
                            {
                               
                                PopAndPushStep(STEP.Write_Barcode_To_Register);
                            }
                        }
                        break;
                    case STEP.Write_Barcode_To_Register:
                        if (nCmdScanbarcode1 == 1)
                        {
                          
                            strBarcode1 = BarcodeScanner1.Getbarcode();
                            PLC.WriteString("R20", strBarcode1);
                        }
                        if (nCmdScanbarcode2 == 1)
                        {
                            
                            strBarcode2 = BarcodeScanner1.Getbarcode();
                            PLC.WriteString("44", strBarcode2);
                        }
                        PopAndPushStep(STEP.Write_Scan_Result);
                        break;
                    case STEP.Write_Scan_Result:
                        if (nCmdScanbarcode1 == 1)
                        {
                            if (Prescription.BarcodeLength == strBarcode1.Length)
                                PLC.WriteInt("19", 2);    //条码1结果码1结果
                            else
                                PLC.WriteInt("19", 1);    //条码1结果码1结果

                            PLC.WriteInt("18", 2);                   
                        }
                        if (nCmdScanbarcode2 == 1)
                        {
                            if (Prescription.BarcodeLength == strBarcode2.Length)
                                PLC.WriteInt("43", 2);    //条码2结果
                            else
                                PLC.WriteInt("43", 1);    //条码2结果
                            PLC.WriteInt("42", 2);
                        }
                        PopAndPushStep(STEP.Check_Enable_Adjust_Laser_Power);
                        break;
                    #endregion

                    #region >>>>调激光功率
                    case STEP.Check_Enable_Adjust_Laser_Power:
                        if (Prescription.AdjustLaser)
                        {
                            ShowPower(EnumUnit.μW, true);   //实时显示功率
                        }
                        else
                        {
                            PopAndPushStep(STEP.INIT);//如果此工序不测试，直接跳到开始
                        }
                        break;
                    case STEP.Wait_Adjust_Laser_Power_Cmd:
                        nCmdAdjustLaser1 = PLC.ReadInt("R66");
                        nCmdAdjustLaser2 = PLC.ReadInt("R81");
                        if (nCmdAdjustLaser1 == 1 || nCmdAdjustLaser1 == 1)
                        {
                            Thread.Sleep(200);  //消抖
                            nCmdAdjustLaser1 = PLC.ReadInt("R66");
                            nCmdAdjustLaser2 = PLC.ReadInt("R81");
                            if (nCmdAdjustLaser1 == 1 || nCmdAdjustLaser1 == 1)
                            {
                                PopAndPushStep(STEP.Adjust_Power);
                            }
                        }
                        break;
                    case STEP.Adjust_Power:     //调整激光
                        bAdjustLaser1Ok = null;
                        bAdjustLaser2Ok = null;
                        if (1 == nCmdAdjustLaser1)
                        {
                            AdjustPowerProcess(1);
                        }
                        if (1 == nCmdAdjustLaser2)
                        {
                            AdjustPowerProcess(2);
                        }

                        PopAndPushStep(STEP.Write_Adjust_Laser_Power_Result);
                        
                        break;
  
                    case STEP.Write_Adjust_Laser_Power_Result:
                        if ((1 == nCmdAdjustLaser1 && bAdjustLaser1Ok.HasValue) && (1 == nCmdAdjustLaser2 && bAdjustLaser2Ok.HasValue))
                        {
                            if (1 == nCmdAdjustLaser1 && bAdjustLaser1Ok.HasValue)
                                PLC.WriteInt("67", (bool)bAdjustLaser1Ok ? 1 : 0);
                            if (1 == nCmdAdjustLaser2 && bAdjustLaser2Ok.HasValue)
                                PLC.WriteInt("82", (bool)bAdjustLaser2Ok ? 1 : 0);
                            PopAndPushStep(STEP.INIT);
                        }
                        else
                            PopAndPushStep(STEP.INIT);  
                        ShowPower(EnumUnit.μW, false);  //关闭激光调整
                        break;
                    #endregion

                    case STEP.DO_NOTHING:   //调试使用
                        ShowInfo("就绪");
                        Thread.Sleep(100);
                        break;

                    case STEP.EMG:
                        ClearAllStep();
                        break;
                    case STEP.EXIT:
                        ShowPower(EnumUnit.μW, false);
                        return 0;
                }
            }
            ShowPower(EnumUnit.μW, false);
            return 0;
        }

        private async void ShowPower(EnumUnit unit, bool bMonitor = true)   //这个监控是两个一起监控
        {
            if (bMonitor)
            {
                if (ctsMonitorPower == null)
                {
                    ctsMonitorPower = new CancellationTokenSource();
                    await Task.Run(() =>
                    {
                        StringBuilder sb = new StringBuilder();
                        while (!ctsMonitorPower.IsCancellationRequested)
                        {
                            sb.Clear();
                            sb.Append(Math.Round(Pw1000USB_1.GetPowerValue(EnumUnit.μW), 3).ToString());
                            sb.Append(" ");
                            sb.Append(unit.ToString());
                            sb.Append(",");
                            sb.Append(Math.Round(Pw1000USB_1.GetPowerValue(EnumUnit.μW), 3).ToString());
                            sb.Append(unit.ToString());
                            Messenger.Default.Send<Tuple<string, string, string>>(new Tuple<string, string, string>(cfg.Name, "ShowPower", sb.ToString()), "WorkFlowMessage");
                        }
                    }, ctsMonitorPower.Token);
                }
            }
            else
            {
                if (ctsMonitorPower != null)
                {
                    ctsMonitorPower.Cancel();
                    ctsMonitorPower = null;
                }
            }
        }
        /// <summary>
        /// 1-解锁lds1,   2-解锁lds2，   3-解锁lds1和lds2
        /// </summary>
        /// <param name="nIndex"></param>
        private void SendUnlockReq(int nIndex)
        {
            if (nIndex < 1 || nIndex > 3)
                throw new Exception($"nIndex now is {nIndex},must be range in [1,3]");
            if (!Directory.Exists(@"c:\ldsDropbox"))
                Directory.CreateDirectory(@"c:\ldsDropbox");
            if (!Directory.Exists(@"c:\ldsTemp"))
                Directory.CreateDirectory(@"c:\ldsTemp");

            File.WriteAllText(@"c:\ldsTemp\UnlockReq.txt", $"Unlock,{nIndex}");
            File.Copy(@"c:\ldsTemp\UnlockReq.txt", @"c:\ldsDropbox\UnlockReq.txt");
        }
        private string[] GetUnlockResult()
        {
            if (File.Exists(@"c:\ldsDropbox\UnlockResult.txt"))
            {
                string[] strResult = File.ReadAllText(@"c:\ldsDropbox\UnlockResult.txt").Split(',');
                File.Delete(@"c:\ldsDropbox\UnlockResult.txt");
                return strResult;   //对方将返回OK,NG的随机组合，中间以逗号隔开
            }
            return null;
        } 
        private async void  AdjustPowerProcess(int nIndex)
        {
            await Task<bool?>.Run(() => {
                bool? bRet = null;
                if (nIndex < 1 || nIndex > 2)
                    throw new Exception($"nIndex now is {nIndex},must be range in [1,2]");
                LDS lds = nIndex == 1 ? lds1 : lds2;
                PowerMeter powerMeter=nIndex == 1 ? Pw1000USB_1 : Pw1000USB_2;
                double powerValue = powerMeter.GetPowerValue(EnumUnit.μW);
                bool bIncrease = false;
                if (powerValue < Prescription.LDSPower[0])
                    bIncrease = true;
                if (powerValue > Prescription.LDSPower[1])
                    bIncrease = false;
                while (powerValue < Prescription.LDSPower[0] || powerValue > Prescription.LDSPower[1])  //直到功率满足要求
                {
                    Thread.Sleep(100);
                    lds.InCreasePower(bIncrease);
                }
                bRet = true;
                if (nIndex == 1)
                    bAdjustLaser1Ok = bRet;
                if (nIndex == 2)
                    bAdjustLaser2Ok = bRet;
            });
        }
    }
}
