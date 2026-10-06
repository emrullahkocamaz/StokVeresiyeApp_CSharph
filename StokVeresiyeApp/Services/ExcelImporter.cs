namespace StokVeresiyeApp.Services;

[Obsolete("Use ExcelService instead")]
public static class ExcelImporter
{
    public static (int products, int stock, int accounts, int accountMovements) Import(string path, bool clearExisting)
    {
        return ExcelService.ImportFromExcel(path, clearExisting);
    }
}
