using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace CurseForgeInstanceCleaner
{
    public class MainForm : Form
    {
        const string Title = "CurseForge Instance Cleaner";

        string path;
        readonly DataGridView grid;
        readonly ToolStripStatusLabel status;

        public MainForm(string path)
        {
            this.path = path;
            Text = Title;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(1000, 540);
            MinimumSize = new Size(720, 380);
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var intro = new Label
            {
                Dock = DockStyle.Top,
                Height = 56,
                Padding = new Padding(10, 10, 10, 0),
                Text = "Tick the game instances to take off CurseForge's list, then click Remove ticked. " +
                       "If CurseForge is open, you'll be asked before it is closed.\n" +
                       "Game and addon folders on your drive are not touched. A backup of the list is made each time."
            };

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None
            };
            grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Remove", FillWeight = 12 });
            AddColumn("Name", 28);
            AddColumn("Game", 22);
            AddColumn("Addons", 10);
            AddColumn("Managed", 11);
            AddColumn("Folder found", 13);
            AddColumn("Folder", 80);
            grid.CurrentCellDirtyStateChanged += delegate
            {
                if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(6) };
            AddButton(buttons, "Remove ticked", RemoveTicked);
            AddButton(buttons, "Refresh", delegate { Reload(); });
            AddButton(buttons, "Restore a backup...", RestoreBackup);
            AddButton(buttons, "Open file location", OpenLocation);
            AddButton(buttons, "Use another file...", ChooseFile);

            var strip = new StatusStrip();
            status = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            strip.Items.Add(status);

            Controls.Add(grid);
            Controls.Add(intro);
            Controls.Add(buttons);
            Controls.Add(strip);

            Shown += delegate { Reload(); };
        }

        void AddColumn(string header, int weight)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header, FillWeight = weight, ReadOnly = true });
        }

        static void AddButton(Control parent, string text, EventHandler click)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 30, Padding = new Padding(4, 0, 4, 0) };
            b.Click += click;
            parent.Controls.Add(b);
        }

        // Only CurseForge's own files need it closed; copies elsewhere can be edited any time.
        bool IsLiveFile
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CurseForge");
                try { return Path.GetFullPath(path).StartsWith(dir + "\\", StringComparison.OrdinalIgnoreCase); }
                catch { return false; }
            }
        }

        void Reload()
        {
            grid.Rows.Clear();
            InstanceFile data;
            try { data = InstanceFile.Load(path); }
            catch (Exception ex)
            {
                SetStatus(ex.Message.Replace("\n", " "));
                return;
            }
            foreach (var inst in data.Instances)
            {
                int i = grid.Rows.Add(false, inst.Name, inst.Game, inst.Addons,
                    inst.Managed ? "Yes" : "No", inst.FolderExists ? "Yes" : "No", inst.Folder);
                grid.Rows[i].Tag = inst;
                if (!inst.FolderExists) grid.Rows[i].DefaultCellStyle.ForeColor = Color.Firebrick;
            }
            string msg = data.Instances.Count + " instances in " + path;
            if (IsLiveFile && CurseForgeApp.IsRunning()) msg = "CurseForge is open: you'll be asked to close it before anything is changed.   " + msg;
            SetStatus(msg);
        }

        void RemoveTicked(object sender, EventArgs e)
        {
            var keys = new List<string>();
            var names = new StringBuilder();
            int tracked = 0;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (!(row.Cells[0].Value is bool) || !(bool)row.Cells[0].Value) continue;
                var inst = (GameInstance)row.Tag;
                keys.Add(inst.Key);
                names.AppendLine("   " + inst.Name + "   (" + inst.Folder + ")");
                tracked += inst.Addons;
            }
            if (keys.Count == 0) { Warn("Tick at least one instance first."); return; }

            string msg = "Remove these from CurseForge's list?\n\n" + names;
            if (tracked > 0)
                msg += "\nCurseForge will stop tracking " + tracked + " addon(s) in these. The addon files stay on your drive.\n";
            bool running = IsLiveFile && CurseForgeApp.IsRunning();
            if (running) msg += "\nCurseForge is open and will be closed first.";
            if (!Confirm(msg)) return;

            string restart;
            if (!EnsureClosed(running, out restart)) return;
            try
            {
                // Read again: CurseForge may have saved its list while closing.
                string backup = InstanceFile.Load(path).Remove(keys);
                Reload();
                SetStatus("Removed " + keys.Count + ". Backup: " + backup);
            }
            catch (Exception ex)
            {
                Warn(ex.Message);
                Reload();
            }
            OfferRestart(restart);
        }

        void RestoreBackup(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Choose a backup to put back",
                Filter = "Backups|" + Path.GetFileName(path) + ".backup-*|All files|*.*"
            };
            try { dlg.InitialDirectory = Path.GetDirectoryName(path); } catch { }
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            bool running = IsLiveFile && CurseForgeApp.IsRunning();
            string msg = "Put this backup back?\n\n   " + Path.GetFileName(dlg.FileName) +
                         "\n\nThe current list is backed up first.";
            if (running) msg += "\nCurseForge is open and will be closed first.";
            if (!Confirm(msg)) return;

            string restart;
            if (!EnsureClosed(running, out restart)) return;
            try
            {
                InstanceFile.Restore(dlg.FileName, path);
                Reload();
                SetStatus("Restored " + Path.GetFileName(dlg.FileName));
            }
            catch (Exception ex)
            {
                Warn(ex.Message);
            }
            OfferRestart(restart);
        }

        // Closes CurseForge if needed. askedAlready = the confirmation said it would be closed.
        // restart = its exe if it was closed here, so it can be started again afterwards.
        bool EnsureClosed(bool askedAlready, out string restart)
        {
            restart = null;
            if (!IsLiveFile || !CurseForgeApp.IsRunning()) return true;
            if (!askedAlready && !Confirm("CurseForge is open. Close it now? It can't be open while its list is changed.")) return false;

            restart = CurseForgeApp.ExePath();
            SetStatus("Closing CurseForge...");
            Cursor = Cursors.WaitCursor;
            bool closed;
            try { closed = CurseForgeApp.Close(); }
            finally { Cursor = Cursors.Default; }
            if (!closed)
            {
                Warn("CurseForge could not be closed. Quit it from its tray icon, then try again.");
                Reload();
                return false;
            }
            return true;
        }

        void OfferRestart(string exe)
        {
            if (exe == null || CurseForgeApp.IsRunning()) return;
            if (!Confirm("Start CurseForge again?")) return;
            try { CurseForgeApp.Start(exe); }
            catch (Exception ex) { Warn("Could not start CurseForge: " + ex.Message); }
        }

        void OpenLocation(object sender, EventArgs e)
        {
            string dir = Path.GetDirectoryName(path);
            if (Directory.Exists(dir)) System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
            else Warn("Folder not found:\n" + dir);
        }

        void ChooseFile(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "CurseForge instance file|*GameInstance*.json|JSON|*.json" };
            try { dlg.InitialDirectory = Path.GetDirectoryName(path); } catch { }
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            path = dlg.FileName;
            Reload();
        }

        void SetStatus(string text)
        {
            status.Text = text;
            Update();
        }

        bool Confirm(string text)
        {
            return MessageBox.Show(this, text, Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        void Warn(string text)
        {
            MessageBox.Show(this, text, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
