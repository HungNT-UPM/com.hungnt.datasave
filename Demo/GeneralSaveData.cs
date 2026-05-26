using System;

namespace HungNT.Datasave
{
    /// <summary>
    /// Lưu các cài đặt chung của game như ngôn ngữ, âm thanh, rung,...
    /// </summary>
    [Serializable]
    public class GeneralSaveData : BaseSaveData
    {
        public string LanguageCode = "en";

        public bool SfxEnabled = true;

        public bool MusicEnabled = true;

        public bool VibrationEnabled = true;

        public override void OnAfterLoad()
        {
        }
    }
}