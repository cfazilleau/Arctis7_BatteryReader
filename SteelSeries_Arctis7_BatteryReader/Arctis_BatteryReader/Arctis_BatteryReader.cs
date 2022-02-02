﻿/*
 * This is free and unencumbered software released into the public domain.
 *
 * Anyone is free to copy, modify, publish, use, compile, sell, or
 * distribute this software, either in source code form or as a compiled
 * binary, for any purpose, commercial or non-commercial, and by any
 * means.
 *
 * In jurisdictions that recognize copyright laws, the author or authors
 * of this software dedicate any and all copyright interest in the
 * software to the public domain. We make this dedication for the benefit
 * of the public at large and to the detriment of our heirs and
 * successors. We intend this dedication to be an overt act of
 * relinquishment in perpetuity of all present and future rights to this
 * software under copyright law.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 * IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
 * OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
 * ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 */

using Mighty.HID;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Timers;
using System.Windows.Forms;

namespace Arctis_BatteryReader
{
	class Arctis_BatteryReader
	{
		public static bool exit = false;
		public static bool refresh = false;

		public const byte batteryAddress = 0x18; // -> Battery(0 - 100)

		private NotifyIcon icon;
		private Icon[] chargeIcons;

		private HIDDev dev = null;
		private HIDInfo hidInfo = null;
		private System.Timers.Timer updateTimer;

		private readonly Dictionary<short, string> deviceIds = new Dictionary<short, string>() {
			{ 0x1260, "Arctis 7 2017" },
			{ 0x12ad, "Arctis 7 2019" },
			{ 0x1252, "Arctis Pro" },
			{ 0x12b3, "Actris 1 Wireless" },
			{ 0x12c2, "Arctis 9" }
		};

		static void Main(string[] args)
		{
			Arctis_BatteryReader reader = new Arctis_BatteryReader();
			if (!exit)
				Application.Run();
			while (!exit)
			{
				Thread.Sleep(500);
			}
		}

		Arctis_BatteryReader()
		{
			this.InitIcons();
			this.InitHIDDev();

			// Start update Timer
			this.updateTimer = new System.Timers.Timer(5000);
			this.updateTimer.Elapsed += this.OnUpdate;
			this.updateTimer.AutoReset = true;
			this.updateTimer.Enabled = true;
		}

		~Arctis_BatteryReader()
		{
			this.updateTimer.Stop();
			this.updateTimer.Dispose();
			this.icon.Visible = false;
			this.icon.Dispose();
		}

		private void OnUpdate(Object source, ElapsedEventArgs e)
		{
			//Get current batteryCharge
			byte batteryCharge = 0;
			this.ReadBattery(out batteryCharge);

			//Update Tray Icon
			if (batteryCharge >= 0 && batteryCharge <= 100)
			{
				this.icon.Icon = this.chargeIcons[batteryCharge];
				this.icon.Text = $"{deviceIds[hidInfo.Pid]} ({batteryCharge}%)";
			}
		}

		private bool ReadBattery(out byte batteryCharge)
		{
			batteryCharge = 0;
			try
			{
				// Set message to send
				byte[] report = new byte[32];
				report[0] = 0x06;
				report[1] = batteryAddress;

				// Send request
				this.dev.Write(report);

				// Prepare buffer for answer
				byte[] reportIn = new byte[31]; //need 31 (by testing)

				// Read answer
				this.dev.Read(reportIn);

				if (reportIn[0] == 0x06 && reportIn[1] == batteryAddress)
				{
					batteryCharge = reportIn[2];
					return true;
				}
			}
			catch (Exception)
			{   //if the read doesn't work, return false
				return false;
			}

			return false;
		}

		private void InitIcons()
		{
			this.chargeIcons = new Icon[101];
			for (int i = 0; i <= 100; i++)
			{
				try
				{
					this.chargeIcons[i] = new Icon($"Headset_Battery_Icons\\Icons\\{i}.ico");
				}
				catch (Exception)
				{
					exit = true;
					MessageBox.Show($"At least one Icon was not found ({i}.ico). Process exiting.");
					return;
				}
			}

			this.icon = new NotifyIcon();
			this.icon.Text = $"Battery Reader";
			ContextMenu trayMenu = new ContextMenu();
			trayMenu.MenuItems.Add("Refresh", refresh_tray_icon);
			trayMenu.MenuItems.Add("Exit", exit_program);
			this.icon.ContextMenu = trayMenu;

			this.icon.Icon = this.chargeIcons[0];

			this.icon.Visible = true;
		}


		private void InitHIDDev()
		{
			int devnumber = 0;

			this.dev = null;

			var devices = HIDBrowse.Browse();

			// Find all Steelseries Arcis 7 devices
			var devs = (HIDBrowse.Browse()).FindAll(x => x.Vid == 0x1038 && deviceIds.ContainsKey(x.Pid));

			if (devs.Count != 0)
			{
				byte batCharge = 0;
				for (devnumber = 0; devnumber < devs.Count; devnumber++)
				{
					this.dev = new HIDDev();
					this.hidInfo = devs.ElementAt(devnumber);

					dev.Open(this.hidInfo);
					if (this.ReadBattery(out batCharge))
					{
						break;
					}
				}

				if (devnumber >= devs.Count)
				{
					MessageBox.Show("None of the supported Arctis HID Devices responded!");
					exit = true;
				}
				else
				{   //if icons loaded correctly and everything else worked so far, exit is false
					if (!exit)
					{
						//Update Tray Icon
						this.icon.Icon = this.chargeIcons[batCharge];
						this.icon.Text = $"{deviceIds[hidInfo.Pid]} ({batCharge}%)";
					}
				}
			}
			else
			{
				MessageBox.Show("HID Device not found!");
				exit = true;
			}
		}

		private void exit_program(object sender, EventArgs e)
		{
			Application.Exit();
			exit = true;
		}

		private void refresh_tray_icon(object sender, EventArgs e)
		{
			this.InitHIDDev();
			//this.OnUpdate(null, null);
		}
	}
}
