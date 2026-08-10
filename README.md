# com.hungnt.datasave

Lưu/đọc dữ liệu người chơi ra file JSON dưới thư mục persistent, mỗi miền dữ liệu một file.

## Yêu cầu

`com.hungnt.core` 2.0.0, `com.unity.nuget.newtonsoft-json`, và **VContainer** (cài thủ công qua Git URL — xem README của core).

## Cài đặt vào container

```csharp
builder.InstallCore();      // IAppLifecycleService — bắt buộc có trước
builder.InstallDataSave();
```

Service đăng ký dạng entry point nên container tự chạy vòng flush theo chu kỳ và tự `Dispose` khi scope kết thúc.

## Khai báo miền dữ liệu

```csharp
[Serializable]
public class PlayerSaveData : BaseSaveData
{
    public int Level;
    public int Coins;

    protected override string SaveFileStem => "player";   // tuỳ chọn, mặc định là snake_case tên class
}
```

Giữ save model là plain C#. Newtonsoft không serialize được `UnityEngine.Object` hay property vòng lặp.

## Sử dụng

```csharp
public class CoinWallet : MonoBehaviour
{
    [Inject] private IDataSaveService _dataSave;

    public void AddCoins(int amount)
    {
        var data = _dataSave.GetData<PlayerSaveData>();
        data.Coins += amount;
        _dataSave.Save(data);
    }
}
```

## Cơ chế ghi đĩa

`Save()` chỉ **đánh dấu dirty** nên gọi mỗi frame cũng không tốn IO. Service gộp ghi mỗi 5 giây: serialize trên main thread rồi ghi trên background thread.

Ghi là **atomic** — ra file `.tmp` rồi `File.Replace`, crash giữa chừng không làm hỏng save cũ.

Khi app pause hoặc quit, toàn bộ miền đã cache được ghi đồng bộ, kể cả miền không dirty — an toàn trước trường hợp code sửa dữ liệu mà quên gọi `Save()`.

Cần chắc chắn dữ liệu đã nằm trên đĩa ngay (sau IAP chẳng hạn) thì dùng `SaveImmediate(data)`, hoặc `FlushDirty()` cho mọi miền đang dirty.

File là plain text, đọc và sửa được khi debug — không mã hoá. Đây là chủ đích: save local không chống được người quyết tâm cheat, nên ưu tiên dễ debug.

## Editor

**`HungNT/Data Save/Data Save Editor`** để xem và sửa từng miền dữ liệu.

Trong Play mode cửa sổ thao tác trực tiếp lên service của game (resolve từ `LifetimeScope`), nên sửa gì là ăn thẳng vào dữ liệu đang chạy. Ngoài Play mode nó dùng phiên riêng đọc thẳng từ đĩa.

Kèm hai menu phụ: `Open Persistent Data` và `Clear All Data`.
