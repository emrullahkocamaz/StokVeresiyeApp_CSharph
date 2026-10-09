using System.Drawing;
using Krypton.Toolkit;

namespace StokVeresiyeApp.Forms;

public class QuickPurchaseDialog : QuickSaleDialog
{
    public QuickPurchaseDialog(long? initialProductId = null)
        : base("Alış", initialProductId)
    {
    }

    protected override void ApplyOperationTheme()
    {
        base.ApplyOperationTheme();
        this.BackColor = Color.FromArgb(239, 253, 245);
        BtnSave.StateCommon.Back.Color1 = Color.FromArgb(22, 163, 74);
        BtnSave.StateCommon.Back.Color2 = Color.FromArgb(5, 150, 105);
        BtnSave.StateCommon.Content.ShortText.Color1 = Color.White;
        if (_lblTouchTitle != null)
        {
            _lblTouchTitle.ForeColor = Color.FromArgb(22, 101, 52);
        }
        if (_pnlTouch != null)
        {
            _pnlTouch.BackColor = Color.FromArgb(239, 253, 245);
        }
        if (_flowTouchButtons != null)
        {
            _flowTouchButtons.BackColor = Color.FromArgb(239, 253, 245);
        }
    }
}
