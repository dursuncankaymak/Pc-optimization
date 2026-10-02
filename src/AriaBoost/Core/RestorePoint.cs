using System;
using System.Management;
using Microsoft.Win32;

namespace AriaBoost.Core
{
    /// <summary>Windows Sistem Geri Yükleme noktası oluşturur.</summary>
    public static class RestorePoint
    {
        private const string FrequencyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
        private const string FrequencyName = "SystemRestorePointCreationFrequency";

        public sealed class Result
        {
            public bool Success { get; set; }
            public string Message { get; set; }
        }

        public static Result Create(string description)
        {
            // Windows varsayılan olarak 24 saatte en fazla bir nokta oluşturur ve sessizce atlar.
            // Bu sınır yalnızca işlem süresince kaldırılır, sonra eski hâline döner.
            var oldFrequency = Reg.Read(Hive.LocalMachine, FrequencyPath, FrequencyName);
            try
            {
                Reg.Write(Hive.LocalMachine, FrequencyPath, FrequencyName, 0, RegistryValueKind.DWord);
            }
            catch (Exception)
            {
                // Sınır kaldırılamazsa yine de denenir.
            }

            try
            {
                var scope = new ManagementScope(@"\\.\root\default");
                using (var cls = new ManagementClass(scope, new ManagementPath("SystemRestore"), new ObjectGetOptions()))
                using (var args = cls.GetMethodParameters("CreateRestorePoint"))
                {
                    args["Description"] = description;
                    args["RestorePointType"] = 12; // MODIFY_SETTINGS
                    args["EventType"] = 100;       // BEGIN_SYSTEM_CHANGE
                    using (var output = cls.InvokeMethod("CreateRestorePoint", args, null))
                    {
                        uint code = Convert.ToUInt32(output?["ReturnValue"] ?? 1u);
                        if (code == 0) return new Result { Success = true, Message = "Geri yükleme noktası oluşturuldu." };
                        if (code == 1058) return new Result { Message = "Sistem Koruması bu bilgisayarda kapalı olduğu için geri yükleme noktası oluşturulamadı." };
                        return new Result { Message = $"Geri yükleme noktası oluşturulamadı (kod {code})." };
                    }
                }
            }
            catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.InvalidClass || ex.ErrorCode == ManagementStatus.InvalidNamespace)
            {
                return new Result { Message = "Bu Windows sürümünde Sistem Geri Yükleme bulunmuyor." };
            }
            catch (Exception ex)
            {
                return new Result { Message = "Geri yükleme noktası oluşturulamadı: " + ex.Message };
            }
            finally
            {
                try
                {
                    if (oldFrequency == null) Reg.Delete(Hive.LocalMachine, FrequencyPath, FrequencyName);
                    else Reg.Write(Hive.LocalMachine, FrequencyPath, FrequencyName, oldFrequency, RegistryValueKind.DWord);
                }
                catch (Exception) { }
            }
        }
    }
}
