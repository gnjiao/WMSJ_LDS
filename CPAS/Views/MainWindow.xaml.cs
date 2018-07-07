﻿using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Media.Imaging;
using CPAS.Interface;
using System.Threading.Tasks;
using System.Threading;
using System.Data;
using CPAS.Config;
using GalaSoft.MvvmLight.Messaging;
using CPAS.Instrument;
using System.Text;
using System;

namespace CPAS.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Task taskMonitor = null;
        private CancellationTokenSource cts = null;
        private int[] ErrorCodeOldList = new int[15];
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (taskMonitor == null || taskMonitor.IsCompleted || taskMonitor.IsCanceled)   //实时显示部分
            {
                DataTable dtError = ConfigMgr.PLCErrorDataTable;
                QSerisePlc PLC = InstrumentMgr.Instance.FindInstrumentByName("PLC") as QSerisePlc;

                cts = new CancellationTokenSource();
                taskMonitor = new Task(() => {
                    while (!cts.IsCancellationRequested)
                    {
#if TEST
                        int[] errorCodes = new int[] {0x00FF,0xFF00,0x000F};
#else
                        int[] errorCodes = new int[] {
                            PLC.ReadInt("R511"),
                            PLC.ReadInt("R512"),
                            PLC.ReadInt("R513"),
                            PLC.ReadInt("R514"),
                            PLC.ReadInt("R515"),
                            PLC.ReadInt("R516"),
                            PLC.ReadInt("R517"),
                            PLC.ReadInt("R518")
                        };
#endif
                        ShowPLCError(errorCodes, dtError);
                        Thread.Sleep(300);
                  
                    }
                }, cts.Token);
            }
            taskMonitor.Start();
        }
        private void ShowPLCError(int[] ErrorCodeList, DataTable dt)
        {
            StringBuilder sb = new StringBuilder();
            int nLen =Math.Min(dt.Rows.Count, ErrorCodeList.Length);

            for (int i= 0; i < nLen; i++)
            {
                if (ErrorCodeList[i] != ErrorCodeOldList[i])
                {
                    ErrorCodeOldList[i] = ErrorCodeList[i];
                    int code = ErrorCodeList[i];
                    for (int j = 0; j < 16; j++)
                    {
                        if (1 == ((code >> j) & 0x01))
                        {
                            Messenger.Default.Send<string>(dt.Rows[i* 16+j]["ErrorMessage"].ToString(),"ShowPLCError");
                        }
                    }
                }
            }
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            BtnStop.PerformClick();
            //if (cts != null)
            //{
            //    cts.Cancel();
            //    taskMonitor.Wait(2000);
            //}
        }

        private void BtnMin_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
