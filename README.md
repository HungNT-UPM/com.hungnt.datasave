# com.hungnt.datasave

Service lưu / load nhiều **domain** độc lập: mỗi class `BaseSaveData` → một file trong thư mục persistent (mặc định **`DataSave/`**). Serialize bằng **Odin Serialization** (JSON), không dùng Newtonsoft.

## Tính năng

- **`IDataSaveService` / `DataSaveService`** — `GetData<T>()`, reload, tích hợp Service Locator
- **`BaseSaveData`** — subclass định nghĩa schema; `Save()` / load tự động
- **DEBUG** — file `.json` plain text (dễ đọc, **HungNT → DataSave Editor**)
- **Release** — AES-CBC, đuôi **`.datasave`**
- **Tên file** — snake_case từ tên type; override `SaveFileStem` nếu cần
- Editor window chỉnh field runtime với `PropertyTree` (backend Odin)

## Định dạng file

| Build | Trên đĩa |
|-------|-----------|
| Có symbol `DEBUG` | `*.json` (Odin JSON UTF-8) |
| Release | `*.datasave` (AES bọc cùng payload JSON) |

Đổi schema khi dev: xóa folder `DataSave/` trong `Application.persistentDataPath` — không có auto-migrate.

## Demo

Assembly **`HungNT.DataSave.Demo`**:

- `GeneralSaveData` — ví dụ domain
- `MultiDomainDatasaveDemo` — lấy nhiều domain qua `IDataSaveService`

```csharp
public class GeneralSaveData : BaseSaveData
{
    public int Level = 1;
    public int Coin;
}

var save = ServiceLocator.Instance.Get<IDataSaveService>();
var general = save.GetData<GeneralSaveData>();
general.Coin += 100;
general.Save();
```

**Editor:** **HungNT > DataSave Editor** — chỉnh và reload domain trực tiếp.
