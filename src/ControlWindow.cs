using System;
using System.Drawing;
using System.Windows.Forms;

namespace DeskFrame {
    internal sealed class ControlWindow : Form {
        internal readonly Label Status=new Label{AutoSize=true,MaximumSize=new Size(278,0),Margin=new Padding(3,3,3,12)};
        internal readonly Button ToggleButton=new Button{AutoSize=true};
        internal readonly Button PauseButton=new Button{AutoSize=true};
        internal readonly Button SourceButton=new Button{Text="Change Pictures...",AutoSize=true};
        internal readonly Button SettingsButton=new Button{Text="Settings...",AutoSize=true};
        internal readonly Label ShortcutStatus=new Label{AutoSize=true,MaximumSize=new Size(278,0),AccessibleName="Shortcut status"};
        internal readonly Button MoveLeftButton=MoveButton("Left"),MoveUpButton=MoveButton("Up"),MoveRightButton=MoveButton("Right"),MoveDownButton=MoveButton("Down");
        static Button MoveButton(string direction) {
            return new Button{Text=direction,AutoSize=true,AccessibleName="Move frame "+direction.ToLowerInvariant(),
                AccessibleDescription="Move the photo frame 20 pixels "+direction.ToLowerInvariant()+", within the selected screen."};
        }
        internal readonly Button ExitButton=new Button{Text="Exit DeskFrame",AutoSize=true};
        internal ControlWindow(Controller app) {
            Text="DeskFrame Controls";Font=new Font("Segoe UI",9);AutoScaleMode=AutoScaleMode.Dpi;
            StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedDialog;
            MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=true;ClientSize=new Size(310,Math.Min(580,Screen.FromControl(this).WorkingArea.Height-80));
            var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(16),AutoScroll=true};
            Controls.Add(panel);
            Status.Font=new Font(Font,FontStyle.Bold);panel.Controls.Add(Status);
            panel.Controls.Add(ToggleButton);panel.Controls.Add(PauseButton);
            panel.Controls.Add(new Label{Text="Move frame (20 pixels per click)",AutoSize=true,MaximumSize=new Size(278,0),Margin=new Padding(3,12,3,3)});
            var movePanel=new TableLayoutPanel{AutoSize=true,ColumnCount=3,RowCount=2,Margin=new Padding(0)};
            movePanel.Controls.Add(MoveUpButton,1,0);movePanel.Controls.Add(MoveLeftButton,0,1);
            movePanel.Controls.Add(MoveDownButton,1,1);movePanel.Controls.Add(MoveRightButton,2,1);
            panel.Controls.Add(movePanel);panel.Controls.Add(ShortcutStatus);
            panel.Controls.Add(SourceButton);panel.Controls.Add(SettingsButton);panel.Controls.Add(ExitButton);
            MoveLeftButton.Click+=delegate {app.Frame.MoveFrame(-20,0);};
            MoveUpButton.Click+=delegate {app.Frame.MoveFrame(0,-20);};
            MoveRightButton.Click+=delegate {app.Frame.MoveFrame(20,0);};
            MoveDownButton.Click+=delegate {app.Frame.MoveFrame(0,20);};
            ToggleButton.Click+=delegate {app.Frame.Toggle();RefreshState(app.Frame);};
            PauseButton.Click+=delegate {app.Frame.SetPaused(!app.Frame.Paused);app.UpdateMenu();RefreshState(app.Frame);};
            SourceButton.Click+=delegate {app.OpenSettings(true);};
            SettingsButton.Click+=delegate {app.OpenSettings(false);};
            ExitButton.Click+=delegate {app.Shutdown();};
            Activated+=delegate {RefreshState(app.Frame);};
            RefreshState(app.Frame);
        }
        internal void RefreshState(Viewer frame) {
            Status.Text=!frame.HasImage ? "Pictures unavailable. Retrying automatically. Choose Change Pictures to use another source." : frame.IsHidden ? "Frame hidden" : frame.Paused ? "Slideshow paused" : "DeskFrame is running";
            ShortcutStatus.Text=frame.ShortcutWarning;ShortcutStatus.Visible=!String.IsNullOrEmpty(frame.ShortcutWarning);
            foreach(var button in new[]{MoveLeftButton,MoveUpButton,MoveRightButton,MoveDownButton})button.Enabled=frame.HasImage && !frame.IsHidden;
            ToggleButton.Enabled=frame.HasImage;
            ToggleButton.Text=!frame.HasImage ? "Frame unavailable" : frame.IsHidden ? "Show Frame" : "Hide Frame";
            PauseButton.Text=frame.Paused ? "Resume Slideshow" : "Pause Slideshow";
        }
    }
}
