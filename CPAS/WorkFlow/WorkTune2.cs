﻿using CPAS.Config;
using CPAS.Config.SoftwareManager;
using CPAS.Instrument;
using CPAS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CPAS.WorkFlow
{
    public delegate void MonitorValueDelegate(bool bMonitor);
    public class WorkTune2 : WorkFlowBase
    {
        private QSerisePlc PLC = null;
        private LDS lds1 = null;
        private LDS lds2 = null;
        public enum EnumCamID { Cam5=4,Cam6}
        


        private CancellationTokenSource ctsMonitorValue1 = null;
        private CancellationTokenSource ctsMonitorValue2 = null;
        private Task taskMonitorValue1 = null;
        private Task taskMonitorValue2 = null;
        private Dictionary<Int32, int> PosValueDic1 = new Dictionary<Int32, int>();
        private Dictionary<Int32, int> PosValueDic2 = new Dictionary<Int32, int>();

        private Task task1 = null, task2 = null;
        private int nSubWorkFlowState = 0;

        protected override bool UserInit()
        {
            #region >>>>读取模块配置信息，初始化工序Enable信息
            if (GetPresInfomation())
                ShowInfo("加载参数成功");
            else
                ShowInfo("加载参数失败,请确认是否选择参数配方");
            #endregion

            #region >>>>初始化仪表信息
            PLC = InstrumentMgr.Instance.FindInstrumentByName("PLC") as QSerisePlc;
            lds1 = InstrumentMgr.Instance.FindInstrumentByName("LDS[4]") as LDS;
            lds2 = InstrumentMgr.Instance.FindInstrumentByName("LDS[5]") as LDS;
            #endregion

            bool bRet= PLC!=null && lds1!=null && lds2!=null && Prescription!=null;
            bRet = true;
            if(!bRet)
                ShowInfo("初始化失败");

            return bRet;
        }
        public WorkTune2(WorkFlowConfig cfg) : base(cfg)
        {

        }
        protected override int WorkFlow()
        {
            return 0;
        }
        private void LdsWorkFunctionSet1()
        {
            int nIndex = 1;
            int nCmd = 0;
            const string cmdReg = "R107";

            const string Angle_Join_Reg = "R213";
            const string bool_Join_Reg = "R212";

            const string Intensity6m_Reg = "R272";
            const string bool_6m_Reg = "R274";

            while (!cts.IsCancellationRequested)
            {
                bool bRet = false;
                nCmd = PLC.ReadInt(cmdReg);
                switch (nCmd)
                {
                    case 1: //计算对接角度
                        bRet = GetTune2JoinAngle(nIndex, out double Angle);
                        PLC.WriteDint(Angle_Join_Reg, Convert.ToInt32(Angle * 1000));
                        PLC.WriteInt(bool_Join_Reg, bRet ? 2 : 1);
                        PLC.WriteInt(cmdReg, nCmd + 1);
                        break;

                    case 5: //获取强度值
                        bRet = GetLaserIntensityValue(nIndex, out int intensityValue);
                        PLC.WriteDint(Intensity6m_Reg, intensityValue);
                        PLC.WriteInt(bool_6m_Reg, bRet ? 2 : 1);
                        PLC.WriteInt(cmdReg, nCmd + 1);
                        break;
                    case 100:
                        ReadResutFromPLC(nIndex);
                        PLC.WriteInt(cmdReg, nCmd + 1);
                        break;
                    default:
                        break;
                }
                Thread.Sleep(100);
            }
        }
        private void LdsWorkFunctionSet2()
        {
            int nIndex = 2;
            int nCmd = 0;
            const string cmdReg = "R134";

            const string Angle_Join_Reg = "R239";
            const string bool_Join_Reg = "R238";

            const string Intensity6m_Reg = "R293";
            const string bool_6m_Reg = "R295";

            while (!cts.IsCancellationRequested)
            {
                bool bRet = false;
                nCmd = PLC.ReadInt(cmdReg);
                switch (nCmd)
                {
                    case 1: //计算对接角度
                        bRet = GetTune2JoinAngle(nIndex, out double Angle);
                        PLC.WriteDint(Angle_Join_Reg, Convert.ToInt32(Angle * 1000));
                        PLC.WriteInt(bool_Join_Reg, bRet ? 2 : 1);
                        PLC.WriteInt(cmdReg, nCmd + 1);
                        break;

                    case 5: //获取强度值
                        bRet = GetLaserIntensityValue(nIndex, out int intensityValue);
                        PLC.WriteDint(Intensity6m_Reg, intensityValue);
                        PLC.WriteInt(bool_6m_Reg, bRet ? 2 : 1);
                        PLC.WriteInt(cmdReg, nCmd + 1);
                        break;
                    case 100:
                        ReadResutFromPLC(nIndex);
                        PLC.WriteInt(cmdReg, nCmd + 1);
                        break;
                    default:
                        break;
                }
                Thread.Sleep(100);
            }
        }


        private bool GetTune2JoinAngle(int nIndex,out double Angle)
        {
            Angle = 0;
            if (nIndex != 1 && nIndex != 2)
                return false;
            return false;
        }
        private bool GetLaserIntensityValue(int nIndex, out int IntensityValue)
        {
            IntensityValue = 0;
            if (nIndex != 1 && nIndex != 2)
                return false;
            return false;
        }
        private bool ReadResutFromPLC(int nIndex)
        {
            return true;
        }
    }
}
