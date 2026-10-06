using StokVeresiyeApp.Forms;

namespace StokVeresiyeApp;

[Obsolete("Use BaseModernForm instead")]
public abstract class BaseForm : BaseModernForm
{
    public BaseForm() : base("Form", 600, 450) { }
}
