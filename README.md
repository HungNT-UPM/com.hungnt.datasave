# HungNT Datasave (`com.hungnt.datasave`)

Lưu / load nhiều **domain** độc lập: mỗi class `BaseSaveData` → một file trong thư mục persistent (mặc định **`Datasave/`**). Serialize bằng **Odin Serialization** (JSON), không dùng Newtonsoft.

## Tính năng

- **`IDatasaveService` / `DatasaveService`** — `GetData<T>()`, reload, tích hợp Service Locator
- **`BaseSaveData`** — subclass định nghĩa schema; `Save()` / load tự động
- **DEBUG** — file `.json` plain text (dễ đọc, **HungNT → Datasave Editor**)
- **Release** — AES-CBC, đuôi **`.datasave`**
- **Tên file** — snake_case từ tên type; override `SaveFileStem` nếu cần
- Editor window chỉnh field runtime với `PropertyTree` (backend Odin)

## Phụ thuộc

`com.hungnt.core`, **Odin Inspector** (serialization + editor).

## Định dạng file

| Build | Trên đĩa |
|-------|-----------|
| Có symbol `DEBUG` | `*.json` (Odin JSON UTF-8) |
| Release | `*.datasave` (AES bọc cùng payload JSON) |

Đổi schema khi dev: xóa folder `Datasave/` trong `Application.persistentDataPath` — không có auto-migrate.

## Demo

Assembly **`HungNT.Datasave.Demo`**:

- `GeneralSaveData`, `BoosterDomainSaveData` — ví dụ domain
- `MultiDomainDatasaveDemo` — lấy nhiều domain qua `IDatasaveService`

```csharp
public class GeneralSaveData : BaseSaveData
{
    public int Level = 1;
    public int Coin;
}

var save = this.GetService<IDatasaveService>();
var general = save.GetData<GeneralSaveData>();
general.Coin += 100;
general.Save();
```

**Editor:** **Reload Data** trên `DatasaveService`, hoặc **Reload services** trong Datasave Editor window.
