﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.IO.Compression;

namespace SteamCloudFileManager
{
    public partial class MainForm : Form
    {
        IRemoteStorage storage;
        // Item1 = cloud name, Item2 = path on disk
        Queue<Tuple<string, string>> uploadQueue = new Queue<Tuple<string, string>>();

        public MainForm()
        {
            InitializeComponent();
        }

        private void connectButton_Click(object sender, EventArgs e)
        {
            try
            {
                uint appId;
                if (string.IsNullOrWhiteSpace(appIdTextBox.Text))
                {
                    MessageBox.Show(this, "Please enter an App ID.", "Failed to connect", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!uint.TryParse(appIdTextBox.Text.Trim(), out appId))
                {
                    MessageBox.Show(this, "Please make sure the App ID you entered is valid.", "Failed to connect", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                storage = RemoteStorage.CreateInstance(uint.Parse(appIdTextBox.Text));
                //storage = new RemoteStorageLocal("remote", uint.Parse(appIdTextBox.Text));
                refreshButton.Enabled = true;
                uploadButton.Enabled = true;
                refreshButton_Click(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Failed to connect", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void refreshButton_Click(object sender, EventArgs e)
        {
            if (storage == null)
            {
                MessageBox.Show(this, "Not connected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            try
            {
                List<IRemoteFile> files = storage.GetFiles();
                remoteListView.Items.Clear();
                foreach (IRemoteFile file in files)
                {
                    ListViewItem itm = new ListViewItem(new string[] { file.Name, file.Timestamp.ToString(), file.Size.ToString(), file.IsPersisted.ToString(), file.Exists.ToString() }) { Tag = file };
                    remoteListView.Items.Add(itm);
                }
                updateQuota();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Can't refresh." + Environment.NewLine + ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        void updateQuota()
        {
            if (storage == null) throw new InvalidOperationException("Not connected");
            ulong totalBytes, availBytes;
            storage.GetQuota(out totalBytes, out availBytes);
            quotaLabel.Text = string.Format("{0}/{1} bytes used", totalBytes - availBytes, totalBytes);
        }

        private void downloadButton_Click(object sender, EventArgs e)
        {
            if (storage == null)
            {
                MessageBox.Show(this, "Not connected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (remoteListView.SelectedIndices.Count != 1)
            {
                DialogResult result = MessageBox.Show(this, "Are you sure you want to download multiple files?", "Confirmation", MessageBoxButtons.YesNo);
                if (result.Equals(DialogResult.No))
                {
                    return;
                }
            }

            if (remoteListView.SelectedIndices.Count > 1)
            {
                saveFileDialog1.FileName = this.appIdTextBox.Text + "-Files.zip";

                if (saveFileDialog1.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                {
                    List<String> failedDownloads = new List<String>();
                    ZipArchive zip = ZipFile.Open(saveFileDialog1.FileName, ZipArchiveMode.Create);
                    foreach (ListViewItem item in remoteListView.SelectedItems)
                    {
                        IRemoteFile file = item.Tag as IRemoteFile;
                        try
                        {
                            Byte[] data = file.ReadAllBytes();
                            var entry = zip.CreateEntry(file.Name);
                            var stream = entry.Open();
                            stream.Write(data, 0, data.Length);
                            stream.Close();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                            failedDownloads.Add(file.Name);
                        }
                    }
                    zip.Dispose();
                    if (failedDownloads.Count > 0)
                    {
                        String msg = "Some files failed to download.";
                        foreach (var failure in failedDownloads)
                        {
                            msg += "\n" + failure;
                        }
                        MessageBox.Show(this, msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

                    }
                    else
                    {
                        MessageBox.Show(this, "All Files Downloaded Successfully!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                }
            } else
            {
                IRemoteFile file = remoteListView.SelectedItems[0].Tag as IRemoteFile;
                saveFileDialog1.FileName = Path.GetFileName(file.Name);
                if (saveFileDialog1.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllBytes(saveFileDialog1.FileName, file.ReadAllBytes());
                        MessageBox.Show(this, "File downloaded.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "File download failed." + Environment.NewLine + ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                }
            }
        }

        private void createAllSubDirectories(String path)
        {
            String dir = Path.GetDirectoryName(path);
            Directory.CreateDirectory(dir);
        }

        private void deleteButton_Click(object sender, EventArgs e)
        {
            if (storage == null)
            {
                MessageBox.Show(this, "Not connected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (remoteListView.SelectedIndices.Count == 0)
            {
                MessageBox.Show(this, "Please select files to delete.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (MessageBox.Show(this, "Are you sure you want to delete the selected files?", "Confirm deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == System.Windows.Forms.DialogResult.No) return;

            bool allSuccess = true;

            foreach (ListViewItem item in remoteListView.SelectedItems)
            {
                IRemoteFile file = item.Tag as IRemoteFile;
                try
                {
                    bool success = file.Delete();
                    if (!success)
                    {
                        allSuccess = false;
                        MessageBox.Show(this, file.Name + " failed to delete.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                    else
                    {
                        item.Remove();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, file.Name + " failed to delete." + Environment.NewLine + ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }

            updateQuota();
            if (allSuccess) MessageBox.Show(this, "Files deleted.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void remoteListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            downloadButton.Enabled = deleteButton.Enabled = (storage != null && remoteListView.SelectedIndices.Count > 0);
        }

        private void uploadBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;
            List<string> failedFiles = new List<string>();
            while (uploadQueue.Count > 0)
            {
                var uploadItem = uploadQueue.Dequeue();
                IRemoteFile file = storage.GetFile(uploadItem.Item1);
                try
                {
                    byte[] data = File.ReadAllBytes(uploadItem.Item2);
                    if (!file.WriteAllBytes(data))
                        failedFiles.Add(uploadItem.Item1);
                }
                catch (IOException ex)
                {
                    failedFiles.Add(uploadItem.Item1);
                }
            }

            e.Result = failedFiles;
        }

        private void uploadButton_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                disableUploadGui();
                foreach (var selectedFile in openFileDialog1.FileNames)
                {
                    uploadQueue.Enqueue(new Tuple<string, string>(Path.GetFileName(selectedFile).ToLowerInvariant(), selectedFile));
                }
                uploadBackgroundWorker.RunWorkerAsync();
            }
        }

        void disableUploadGui()
        {
            // Disables app switching, refresh, and upload button
            connectButton.Enabled = false;
            refreshButton.Enabled = false;
            uploadButton.Enabled = false;
            uploadButton.Text = "Uploading...";
        }

        void enableUploadGui()
        {
            connectButton.Enabled = true;
            refreshButton.Enabled = true;
            uploadButton.Enabled = true;
            uploadButton.Text = "Upload";
        }

        private void uploadBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var failedList = e.Result as List<string>;
            if (failedList.Count == 0)
            {
                MessageBox.Show(this, "Upload complete.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                failedList.Insert(0, "The following files have failed to upload:");
                MessageBox.Show(this, string.Join(Environment.NewLine, failedList), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            enableUploadGui();
            refreshButton_Click(this, EventArgs.Empty);
        }
    }
}
