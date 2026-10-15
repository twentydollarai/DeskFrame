using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Automation;
using System.Windows.Automation.Provider;

namespace DeskFrame {
    // The legacy WinForms spinner proxy reports zero ranges. Supply its real range
    // at the numeric HWND while retaining ordinary native text/keyboard editing.
    internal sealed class NumericSetting : NumericUpDown {
        RangeProvider provider;
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x3D && m.LParam.ToInt64()==AutomationInteropProvider.RootObjectId) {
                if(provider==null)provider=new RangeProvider(this);
                m.Result=AutomationInteropProvider.ReturnRawElementProvider(Handle,m.WParam,m.LParam,provider);return;
            }
            base.WndProc(ref m);
        }
        protected override void OnValueChanged(EventArgs e) {
            base.OnValueChanged(e);
            if(provider!=null && AutomationInteropProvider.ClientsAreListening)provider.NotifyValue();
        }
        [ComVisible(true),ClassInterface(ClassInterfaceType.None)]
        public sealed class RangeProvider : IRawElementProviderSimple,IRangeValueProvider,IValueProvider {
            readonly NumericSetting owner;
            double previous;
            internal RangeProvider(NumericSetting control){owner=control;previous=(double)control.Value;}
            T Read<T>(Func<T> action){return owner.InvokeRequired?(T)owner.Invoke(action):action();}
            public ProviderOptions ProviderOptions {get{return ProviderOptions.ServerSideProvider;}}
            public IRawElementProviderSimple HostRawElementProvider {get{return Read(()=>AutomationInteropProvider.HostProviderFromHandle(owner.Handle));}}
            public object GetPatternProvider(int id){return id==RangeValuePatternIdentifiers.Pattern.Id || id==ValuePatternIdentifiers.Pattern.Id ? this : null;}
            public object GetPropertyValue(int id){
                if(id==AutomationElementIdentifiers.ControlTypeProperty.Id)return ControlType.Spinner.Id;
                if(id==AutomationElementIdentifiers.NameProperty.Id)return Read(()=>owner.AccessibleName);
                if(id==AutomationElementIdentifiers.IsControlElementProperty.Id || id==AutomationElementIdentifiers.IsContentElementProperty.Id)return true;
                if(id==AutomationElementIdentifiers.IsKeyboardFocusableProperty.Id)return true;
                if(id==AutomationElementIdentifiers.IsEnabledProperty.Id)return Read(()=>owner.Enabled);
                if(id==AutomationElementIdentifiers.HasKeyboardFocusProperty.Id)return Read(()=>owner.ContainsFocus);
                return null;
            }
            public bool IsReadOnly {get{return Read(()=>owner.ReadOnly || !owner.Enabled);}}
            public double Minimum {get{return Read(()=>(double)owner.Minimum);}}
            public double Maximum {get{return Read(()=>(double)owner.Maximum);}}
            public double Value {get{return Read(()=>(double)owner.Value);}}
            public double SmallChange {get{return Read(()=>(double)owner.Increment);}}
            public double LargeChange {get{return SmallChange;}}
            public void SetValue(double value){
                Read(delegate {
                    if(!owner.Enabled)throw new ElementNotEnabledException();
                    if(owner.ReadOnly)throw new InvalidOperationException("Setting is read-only.");
                    if(Double.IsNaN(value) || Double.IsInfinity(value) || value<(double)owner.Minimum || value>(double)owner.Maximum)
                        throw new ArgumentOutOfRangeException("value");
                    owner.Value=(decimal)value;return true;
                });
            }
            string IValueProvider.Value {get{return Read(()=>owner.Value.ToString(CultureInfo.CurrentCulture));}}
            void IValueProvider.SetValue(string value){SetValue(Double.Parse(value,NumberStyles.Number,CultureInfo.CurrentCulture));}
            internal void NotifyValue(){
                double value=(double)owner.Value;
                AutomationInteropProvider.RaiseAutomationPropertyChangedEvent(this,new AutomationPropertyChangedEventArgs(RangeValuePatternIdentifiers.ValueProperty,previous,value));
                previous=value;
            }
        }
    }
}
