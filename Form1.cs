using PacketDotNet;
using PacketDotNet.Ieee80211;
using SharpPcap;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace NetworkTrafficAnalyzer_Project
{
    public partial class Form1 : Form
    {
        // A class-level variable to hold the list of network devices.
        private CaptureDeviceList devices;
        // The currently selected device for capturing.
        private ICaptureDevice selectedDevice;
        // A counter for the packet number.
        private int packetCounter = 1;

        public Form1()
        {
            InitializeComponent();
            InitializeDeviceList();
        }

        /// <summary>
        /// Populates the ComboBox with the list of available network devices.
        /// </summary>
        private void InitializeDeviceList()
        {
            // Retrieve the device list
            devices = CaptureDeviceList.Instance;

            // Ensure that devices were found
            if (devices.Count < 1)
            {
                MessageBox.Show("No network devices found. Make sure Npcap is installed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }

            // Add each device to the combo box
            foreach (var dev in devices)
            {
                // The description is usually more human-readable than the name.
                cmbDevices.Items.Add(dev.Description);
            }

            // Select the first device by default.
            if (cmbDevices.Items.Count > 0)
            {
                cmbDevices.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Handles the Click event for the "Start Capture" button.
        /// </summary>
        private void BtnStart_Click(object sender, EventArgs e)
        {
            try
            {
                // Get the selected device from the ComboBox.
                selectedDevice = devices[cmbDevices.SelectedIndex];

                // Register our event handler for packet arrivals.
                selectedDevice.OnPacketArrival += OnPacketArrival;

                // Open the device for capturing.
                // promiscuous mode  : true means the device captures all packets on the network, not just those addressed to it.
                // read timeout (ms) : 1000 ms = 1 second.
                int readTimeoutMilliseconds = 1000;
                selectedDevice.Open(DeviceModes.Promiscuous, readTimeoutMilliseconds);

                // Start the capture process.
                selectedDevice.StartCapture();

                // Update UI state.
                btnStart.Enabled = false;
                btnStop.Enabled = true;
                cmbDevices.Enabled = false;
                Packets.Items.Clear(); // Clear previous captures
                packetCounter = 1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting capture: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Handles the Click event for the "Stop Capture" button.
        /// </summary>
        private void BtnStop_Click(object sender, EventArgs e)
        {
            try
            {
                if (selectedDevice != null && selectedDevice.Started)
                {
                    // Stop the capture process.
                    selectedDevice.StopCapture();
                    // Close the device.
                    selectedDevice.Close();
                    // Unregister the event handler.
                    selectedDevice.OnPacketArrival -= OnPacketArrival;
                }
            }
            catch (Exception ex)
            {
                // It's good practice to handle potential exceptions, though less common here.
                MessageBox.Show($"Error stopping capture: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Update UI state.
                btnStart.Enabled = true;
                btnStop.Enabled = false;
                cmbDevices.Enabled = true;
            }
        }

        /// <summary>
        /// This is the core method that gets called for each captured packet.
        /// </summary>
        private void OnPacketArrival(object sender, PacketCapture e)
        {
            // ** FIX **
            // We must copy the data from the packet capture 'e' into local variables
            // before we enter the Invoke delegate. This is because 'e' is a ref-like
            // type and cannot be used in a lambda expression.
            var rawPacket = e.GetPacket();
            var timestamp = e.Header.Timeval.Date;

            // This method runs on a background thread, so we need to use Invoke
            // to update the UI elements, which are on the main thread.
            this.Invoke((MethodInvoker)delegate
            {
                try
                {
                    // Use the local variables (rawPacket, timestamp) inside the Invoke block.
                    var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);

                    // Default values
                    string sourceIp = "N/A";
                    string destIp = "N/A";
                    string protocol = "N/A";

                    // Extract IP packet information
                    // ** FIX: Corrected IPacket to IPPacket **
                    var ipPacket = packet.Extract<IPPacket>();
                    if (ipPacket != null)
                    {
                        sourceIp = ipPacket.SourceAddress.ToString();
                        destIp = ipPacket.DestinationAddress.ToString();
                        protocol = ipPacket.Protocol.ToString();
                    }

                    // Create a new ListView item
                    var item = new ListViewItem(packetCounter.ToString());
                    item.SubItems.Add(timestamp.ToLongTimeString()); // Use the copied timestamp
                    item.SubItems.Add(sourceIp);
                    item.SubItems.Add(destIp);
                    item.SubItems.Add(protocol);
                    item.SubItems.Add(rawPacket.Data.Length.ToString());

                    // Add the item to the ListView
                    Packets.Items.Add(item);

                    // Increment the packet counter
                    packetCounter++;
                }
                catch (Exception ex)
                {
                    // Handle any exceptions during packet processing.
                    // This can prevent the application from crashing on malformed packets.
                    Console.WriteLine($"Packet processing error: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Ensures the capture is stopped when the form is closed.
        /// </summary>
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (selectedDevice != null && selectedDevice.Started)
            {
                BtnStop_Click(null, null); // Gracefully stop the capture.
            }
        }
    }
}