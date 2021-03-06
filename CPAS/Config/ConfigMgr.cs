﻿using CPAS.Classes;
using CPAS.Config.HardwareManager;
using CPAS.Config.PrescriptionManager;
using CPAS.Config.SoftwareManager;
using CPAS.Config.SystemCfgManager;
using CPAS.Config.UserManager;
using CPAS.Instrument;
using CPAS.Models;
using CPAS.WorkFlow;
using GalaSoft.MvvmLight.Messaging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CPAS.Config
{
    public enum EnumConfigType
    {
        HardwareCfg,
        SoftwareCfg,
        PrescriptionCfg,
        UserCfg,
        SystemParaCfg,
    }
    public class ConfigMgr
    {
        private ConfigMgr()
        {

        }
        private static readonly Lazy<ConfigMgr> _instance = new Lazy<ConfigMgr>(() => new ConfigMgr());
        public static ConfigMgr Instance
        {
            get { return _instance.Value; }
        }
        private readonly string File_HardwareCfg = FileHelper.GetCurFilePathString() + "Config\\HardwareCfg.json";
        private readonly string File_SoftwareCfg = FileHelper.GetCurFilePathString() + "Config\\SoftwareCfg.json";
        private readonly string File_UserCfg = FileHelper.GetCurFilePathString() + "User.json";
        private readonly string File_PLCError = FileHelper.GetCurFilePathString() + "Config\\PLCError.xls";
        private readonly string File_PrescriptionCfg = FileHelper.GetCurFilePathString() + "Config\\PrescriptionCfg.json";
        private readonly string File_SystemParaCfg = FileHelper.GetCurFilePathString() + "Config\\SystemParaCfg.json";


        private LogExcel logexcel = null;
        public static HardwareCfgManager HardwareCfgMgr = null;
        public static SoftwareCfgManager SoftwareCfgMgr = null;
        public static PrescriptionCfgManager PrescriptionCfgMgr = null;
        public static UserCfgManager UserCfgMgr = null;
        public static SystemParaCfgManager SystemParaCfgMgr =null;
        public static DataTable PLCErrorDataTable = new DataTable();
        public static CameraCfg[] CameraCfgs = null;
        //public static 
        public void LoadConfig()
        {
            #region >>>>Hardware init
            try
            {
                var json_string = File.ReadAllText(File_HardwareCfg);
                HardwareCfgMgr = JsonConvert.DeserializeObject<HardwareCfgManager>(json_string);
                CameraCfgs = HardwareCfgMgr.CameraCfgs;
            }
            catch (Exception ex)
            {
                Messenger.Default.Send<string>(String.Format("Unable to load config file {0}, {1}", File_HardwareCfg, ex.Message), "ShowError");
                throw new Exception(ex.Message);
            }
            InstrumentBase inst = null;
            HardwareCfgLevelManager1[] instCfgs = null;
            string strClassName = "";
            Type t = HardwareCfgMgr.GetType();
            PropertyInfo[] PropertyInfos = t.GetProperties();
            for (int i = 0; i < PropertyInfos.Length; i++)
            {
                if (PropertyInfos[i].Name.ToUpper().Contains("COMPORT") || PropertyInfos[i].Name.ToUpper().Contains("ETHERNET") ||
                    PropertyInfos[i].Name.ToUpper().Contains("GPIB") || PropertyInfos[i].Name.ToUpper().Contains("NIVISA") ||
                     PropertyInfos[i].Name.ToUpper().Contains("CAMERACFG"))
                    continue;
                PropertyInfo pi = PropertyInfos[i];
                instCfgs = pi.GetValue(HardwareCfgMgr) as HardwareCfgLevelManager1[];
                strClassName = pi.Name.Substring(0, pi.Name.Length - 1);
                foreach (var it in instCfgs)
                {
                    if (!it.Enabled)
                        continue;
                    inst = t.Assembly.CreateInstance("CPAS.Instrument." + strClassName, true, BindingFlags.CreateInstance, null, new object[] { it }, null, null) as InstrumentBase;
                    if (inst != null && it.Enabled)
                    {
                        if (inst.Init())
                            InstrumentMgr.Instance.AddInstrument(it.InstrumentName, inst);
                        else
                            Messenger.Default.Send<string>(string.Format("Instrument :{0} init failed", it.InstrumentName), "ShowError");
                    }
                }
            }
            #endregion

            #region >>>>Software init
            try
            {
                var json_string = File.ReadAllText(File_SoftwareCfg);
                SoftwareCfgMgr = JsonConvert.DeserializeObject<SoftwareCfgManager>(json_string);
            }
            catch (Exception ex)
            {
                Messenger.Default.Send<string>(String.Format("Unable to load config file {0}, {1}", File_SoftwareCfg, ex.Message), "ShowError");
                throw new Exception(ex.Message);
            }

            Type tStationCfg = SoftwareCfgMgr.GetType();
            PropertyInfo[] pis = tStationCfg.GetProperties();
            SoftwareManager.WorkFlowConfig[] WorkFlowCfgs = null;
            WorkFlowBase workFlowBase = null;
            foreach (PropertyInfo pi in pis)
            {
                WorkFlowCfgs = pi.GetValue(SoftwareCfgMgr) as SoftwareManager.WorkFlowConfig[];
                foreach (var it in WorkFlowCfgs)
                {
                    if (it.Enable)
                    {
                        workFlowBase = tStationCfg.Assembly.CreateInstance("CPAS.WorkFlow." + it.Name, true, BindingFlags.CreateInstance, null, new object[] { it }, null, null) as WorkFlowBase;
                        WorkFlowMgr.Instance.AddStation(it.Name, workFlowBase);
                    }
                }
            }
            #endregion

            #region >>>>PrescriptionCfg Init
            try
            {
                var json_string = File.ReadAllText(File_PrescriptionCfg);
                PrescriptionCfgMgr = JsonConvert.DeserializeObject<PrescriptionCfgManager>(json_string);
            }
            catch (Exception ex)
            {
                Messenger.Default.Send<string>(String.Format("Unable to load config file {0}, {1}", File_SoftwareCfg, ex.Message), "ShowError");
                throw new Exception(ex.Message);
            }
            #endregion

            #region >>>>SystemCfg
            try
            {
                var json_string = File.ReadAllText(File_SystemParaCfg);
                SystemParaCfgMgr = JsonConvert.DeserializeObject<SystemParaCfgManager>(json_string);
            }
            catch (Exception ex)
            {
                Messenger.Default.Send<string>(String.Format("Unable to load config file {0}, {1}", File_SystemParaCfg, ex.Message), "ShowError");
                throw new Exception(ex.Message);
            }
            #endregion

            #region >>>> UserCfg init
            try
            {
                var json_string = File.ReadAllText(File_UserCfg);
                UserCfgMgr = JsonConvert.DeserializeObject<UserCfgManager>(json_string);
            }
            catch (Exception ex)
            {
                Messenger.Default.Send<string>(String.Format("Unable to load config file {0}, {1}", File_UserCfg, ex.Message), "ShowError");
                throw new Exception(ex.Message);
            }
            #endregion

            #region >>>>PLCError Init
            logexcel = new LogExcel(File_PLCError);
            logexcel.ExcelToDataTable(ref PLCErrorDataTable, "Sheet1");
            #endregion
        }
        public void SaveConfig(EnumConfigType cfgType, object[] listObj)
        {
            if (listObj == null)
                throw new Exception(string.Format("保存的{0}数据为空", cfgType.ToString()));
            string fileSaved = null;
            object objSaved = null;
            switch (cfgType)
            {
                case EnumConfigType.HardwareCfg:
                    fileSaved = File_HardwareCfg;
                    //objSaved=new HardwareCfgManager() {  }
                    break;
                case EnumConfigType.PrescriptionCfg:
                    fileSaved = File_PrescriptionCfg;
                    objSaved = new PrescriptionCfgManager();
                    (objSaved as PrescriptionCfgManager).Prescriptions = listObj as PrescriptionGridModel[];
                    PrescriptionCfgMgr = objSaved as PrescriptionCfgManager;
                    break;
                case EnumConfigType.SoftwareCfg:
                    fileSaved = File_SoftwareCfg;
                    break;
                case EnumConfigType.UserCfg:
                    fileSaved = File_UserCfg;
                    objSaved = new UserCfgManager();
                    (objSaved as UserCfgManager).Users = listObj as UserModel[];
                    UserCfgMgr = objSaved as UserCfgManager;
                    break;
                case EnumConfigType.SystemParaCfg:
                    fileSaved = File_SystemParaCfg;
                    objSaved = new SystemParaCfgManager();
                    (objSaved as SystemParaCfgManager).SystemParaModels = listObj as SystemParaModel[];
                    SystemParaCfgMgr = objSaved as SystemParaCfgManager;
                    break; 
                default:
                    break;
            }
            string json_str = JsonConvert.SerializeObject(objSaved);
            File.WriteAllText(fileSaved, json_str);
            //using (FileStream fs = File.Open(fileSaved, FileMode.Create, FileAccess.Write))
            //{
            //    using (StreamWriter wr = new StreamWriter(fs))
            //    {
            //        wr.Write(json_str);
            //        wr.Close();
            //    }
            //    fs.Flush();
            //    fs.Close();
            //}
        }
    }
}
