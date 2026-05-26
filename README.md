# HungNT Datasave (`com.hungnt.datasave`)

Mỗi lớp `BaseSaveData` = một file trong thư mục con persistent (mặc định **`Datasave/`**). **Serialize/Deserialize:** [Sirenix.Serialization](https://odininspector.com/documentation/sirenix.serialization) (`DataFormat.JSON` trong luồng lưu — không dùng Newtonsoft).

## Định dạng file trên đĩa (DEBUG vs release)

- **Build có symbol `DEBUG`**: ghi **chuỗi Odin JSON** UTF-8, đuôi `.json` (mở xem / **HungNT → Datasave Editor**).
- **Build không `DEBUG`**: AES-CBC bọc cùng chuỗi Odin JSON, đuôi **`.datasave`**.

Đổi schema trong lúc phát triển: xóa thư mục **`Datasave/`** trong `persistentDataPath` — không migrate tự động.

## Tên file

- **Stem**: snake_case từ `GetType().Name`; ghim `protected override string SaveFileStem => "my_stem";` (không hậu tố `.json`, không `/` hay `.`).
- **API** `SaveFileName` mặc định `*.json`; trên đĩa release thực tế là **`*.datasave`**.

Partial demo `GeneralSaveData`: `Runtime/Demo/GeneralSaveData.DemoClient.cs`.

## Datasave Editor Window

`PropertyTree` dùng **`SerializationBackend.Odin`** để vẽ đúng kiểu phức tạp (ví dụ `Dictionary<enum, struct>`) thống nhất với format file.

## Đồng bộ Editor / Play Mode

- **Play Mode**: **Reload Data** trên `DatasaveService`, hoặc **Reload services** trong cửa sổ editor.
- **Reload Data** chỉ reload các miền đã từng `GetData` trong session.

## Phụ thuộc

**`com.hungnt.core`**, **`com.hungnt.odininspector`** (Serialization + Inspector/Editor). Schema do từng `BaseSaveData` định nghĩa.
