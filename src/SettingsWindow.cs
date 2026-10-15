using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;

namespace DeskFrame {
    internal sealed class SettingsWindow : Form {
        internal readonly Button FolderButton=new Button{Text="Choose folder...",AutoSize=true,Name="ChooseFolder"};
        internal readonly Button FilesButton=new Button{Text="Choose images...",AutoSize=true,Name="ChooseFiles"};
        internal readonly Button SaveButton=new Button{Text="Save",AutoSize=true,Name="SaveSettings"};
        internal readonly TextBox SourceText=new TextBox{ReadOnly=true,Multiline=true,Height=54,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,Name="ImageSource"};
        internal readonly NumericUpDown FrameWidth=Number(80,1600),OpacityValue=Number(10,100),Interval=Number(2,3600),EdgeMargin=Number(0,500);
        internal readonly ComboBox Corner=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        internal readonly CheckBox Topmost=Check("Always on top"),ClickThrough=Check("Let mouse clicks pass through"),Fade=Check("Fade between images"),StartupBox=Check("Start with Windows"),Remember=Check("Remember position between runs"),TrayBox=Check("Show notification-area icon");
        internal readonly TextBox Hotkey=new TextBox{Dock=DockStyle.Fill,Name="Hotkey"};
        internal readonly Label Error=new Label{AutoSize=true,ForeColor=Color.Firebrick,MaximumSize=new Size(520,0)};
        internal readonly ComboBox Sizing=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill},Fitting=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill},MonitorChoice=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        readonly string[] monitors;
        Preferences draft;
        readonly Func<Preferences,string> apply;
        internal SettingsWindow(Preferences value,bool source,bool portable,Func<Preferences,string> save) {
            draft=value.Copy();apply=save;
            Text=ImageSource.Find(value).Length==0 ? "Welcome to DeskFrame" : "DeskFrame Settings";
            Font=new Font("Segoe UI",9);AutoScaleMode=AutoScaleMode.Dpi;
            StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
            ClientSize=new Size(590,Math.Min(790,Screen.FromControl(this).WorkingArea.Height-80));
            var panel=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=2,AutoScroll=true};
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,155));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            Controls.Add(panel);
            AddWide(panel,new Label{Text="Your pictures. A quiet place on your desktop.",AutoSize=true,Font=new Font(Font,FontStyle.Bold),Margin=new Padding(0,0,0,12)});
            AddWide(panel,new Label{Text="Open DeskFrame.exe again for controls or to exit, even with the tray icon off.",AutoSize=true,MaximumSize=new Size(520,0),Name="ControlRecoveryHint"});
            AddRow(panel,"Image source",SourceText);
            var choose=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};choose.Controls.Add(FolderButton);choose.Controls.Add(FilesButton);AddRow(panel,"",choose);
            Sizing.Items.AddRange(new object[]{"Auto-fit","Fixed frame"});AddRow(panel,"Frame sizing",Sizing);
            Fitting.Items.AddRange(new object[]{"Fit","Fill"});AddRow(panel,"Image fitting",Fitting);
            Sizing.SelectedItem=value.FrameSizing;Fitting.SelectedItem=value.ImageFitting;Fitting.Enabled=value.FrameSizing=="Fixed frame";
            Sizing.SelectedIndexChanged+=delegate {Fitting.Enabled=(string)Sizing.SelectedItem=="Fixed frame";};
            monitors=new[]{""}.Concat(Screen.AllScreens.Select(s=>s.DeviceName)).ToArray();
            MonitorChoice.Items.Add("Primary monitor");
            foreach(var screen in Screen.AllScreens)MonitorChoice.Items.Add(screen.DeviceName+" ("+screen.Bounds.Width+" x "+screen.Bounds.Height+")");
            MonitorChoice.SelectedIndex=Math.Max(0,Array.IndexOf(monitors,value.Monitor));AddRow(panel,"Monitor",MonitorChoice);
            AddRow(panel,"Max width (px)",FrameWidth);AddRow(panel,"Opacity (%)",OpacityValue);AddRow(panel,"Interval (seconds)",Interval);
            Corner.Items.AddRange(new object[]{"bottom-right","bottom-left","top-right","top-left"});AddRow(panel,"Screen corner",Corner);AddRow(panel,"Edge margin (px)",EdgeMargin);
            AddWide(panel,Topmost);AddWide(panel,ClickThrough);AddWide(panel,Fade);AddWide(panel,Remember);AddWide(panel,TrayBox);
            AddWide(panel,new Label{Text="Icon off? Open DeskFrame.exe again, then choose Exit DeskFrame to quit.",AutoSize=true,MaximumSize=new Size(520,0)});AddWide(panel,StartupBox);
            AddRow(panel,"Show/hide shortcut",Hotkey);
            AddWide(panel,new Label{Text="Example: Ctrl+Alt+D. Leave blank to disable.\nMove frame: use Controls, or Ctrl+Alt+Shift+Arrow when available. Open DeskFrame.exe again for controls.",AutoSize=true,MaximumSize=new Size(520,0)});
            if(portable)AddWide(panel,new Label{Text="Portable mode: preferences stay in DeskFrameData beside this program.\nEnabling Windows startup still registers this executable for your account.",AutoSize=true,MaximumSize=new Size(520,0)});
            AddWide(panel,Error);
            var buttons=new FlowLayoutPanel{FlowDirection=FlowDirection.RightToLeft,AutoSize=true,Dock=DockStyle.Fill};
            var cancel=new Button{Text="Cancel",AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.Add(SaveButton);buttons.Controls.Add(cancel);buttons.Dock=DockStyle.Bottom;buttons.Padding=new Padding(12);Controls.Add(buttons);panel.BringToFront();
            AcceptButton=SaveButton;CancelButton=cancel;cancel.Click+=delegate {Close();};
            FrameWidth.Value=value.Width;OpacityValue.Value=(decimal)(value.Opacity*100);Interval.Value=(decimal)value.IntervalSeconds;EdgeMargin.Value=value.Margin;
            Corner.SelectedItem=value.Corner;Topmost.Checked=value.Topmost;ClickThrough.Checked=value.ClickThrough;Fade.Checked=value.Crossfade;Remember.Checked=value.RememberPosition;TrayBox.Checked=value.ShowTray;StartupBox.Checked=value.Startup;Hotkey.Text=value.Hotkey;
            RefreshSource();FolderButton.Click+=ChooseFolder;FilesButton.Click+=ChooseFiles;SaveButton.Click+=Save;
            Shown+=delegate {if(source)FolderButton.Focus();};
        }
        static NumericUpDown Number(int min,int max) {return new NumericSetting{Minimum=min,Maximum=max,Dock=DockStyle.Fill,ThousandsSeparator=true};}
        static CheckBox Check(string text) {return new CheckBox{Text=text,AutoSize=true,Margin=new Padding(0,4,0,4)};}
        static void AddWide(TableLayoutPanel p,Control c) {int row=p.RowCount++;p.RowStyles.Add(new RowStyle(SizeType.AutoSize));p.Controls.Add(c,0,row);p.SetColumnSpan(c,2);}
        static void AddRow(TableLayoutPanel p,string text,Control c) {int row=p.RowCount++;p.RowStyles.Add(new RowStyle(SizeType.AutoSize));p.Controls.Add(new Label{Text=text,AutoSize=true,Margin=new Padding(0,7,4,7)},0,row);c.AccessibleName=text;c.Margin=new Padding(0,3,0,3);p.Controls.Add(c,1,row);}
        void ChooseFolder(object sender,EventArgs e) {
            string initial=String.IsNullOrEmpty(draft.Folder) && draft.Files.Length>0 ? System.IO.Path.GetDirectoryName(draft.Files[0]) : draft.Folder;
            using(var picker=new FolderBrowserDialog{Description="Choose the folder of pictures for DeskFrame",ShowNewFolderButton=false,SelectedPath=initial}) {
                if(picker.ShowDialog(this)==DialogResult.OK) {draft.Folder=picker.SelectedPath;draft.Files=new string[0];RefreshSource();}
            }
        }
        void ChooseFiles(object sender,EventArgs e) {
            using(var picker=new OpenFileDialog{Title="Choose pictures for DeskFrame",Filter="Pictures (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif",Multiselect=true,CheckFileExists=true}) {
                if(picker.ShowDialog(this)==DialogResult.OK) {draft.Folder="";draft.Files=picker.FileNames;RefreshSource();}
            }
        }
        internal void RefreshSource() {SourceText.Text=String.IsNullOrEmpty(draft.Folder) ? (draft.Files.Length==0 ? "Choose a folder or one or more pictures to begin." : String.Join(Environment.NewLine,draft.Files)) : draft.Folder;}
        void Save(object sender,EventArgs e) {
            var p=draft.Copy();p.Width=(int)FrameWidth.Value;p.Opacity=(double)OpacityValue.Value/100;p.IntervalSeconds=(double)Interval.Value;p.Margin=(int)EdgeMargin.Value;p.Corner=(string)Corner.SelectedItem;
            p.FrameSizing=(string)Sizing.SelectedItem;p.ImageFitting=(string)Fitting.SelectedItem;p.Monitor=monitors[MonitorChoice.SelectedIndex];
            p.Topmost=Topmost.Checked;p.ClickThrough=ClickThrough.Checked;p.Crossfade=Fade.Checked;p.Startup=StartupBox.Checked;p.RememberPosition=Remember.Checked;p.ShowTray=TrayBox.Checked;p.Hotkey=Hotkey.Text.Trim();
            if(p.Corner!=draft.Corner || p.Margin!=draft.Margin || p.Width!=draft.Width || p.Monitor!=draft.Monitor || !p.RememberPosition) {p.X=null;p.Y=null;p.AnchorX=null;p.AnchorY=null;}
            string error=apply(p);if(error!=null) {Error.Text=error;((ScrollableControl)Error.Parent).ScrollControlIntoView(Error);return;}Close();
        }
    }
}


