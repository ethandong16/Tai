using Core.Event;
using Core.Librarys;
using Core.Models.Config;
using Core.Servicers.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Core.Servicers.Instances
{
    public class AppConfig : IAppConfig
    {
        private string fileName;
        private ConfigModel config;
        private readonly object saveLock = new object();

        public event AppConfigEventHandler ConfigChanged;
        private ConfigModel oldConfig;

        public AppConfig()
        {
            fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "Data",
                "AppConfig.json");
        }
        public void Load()
        {
            string configText = string.Empty;
            try
            {
                if (File.Exists(fileName))
                {
                    configText = File.ReadAllText(fileName);
                    config = JsonConvert.DeserializeObject<ConfigModel>(configText);
                    if (config == null) CreateDefaultConfig();
                }
                else
                {
                    //  创建基础配置
                    CreateDefaultConfig();

                    Save();
                }

                CheckOption(config);
                CopyToOldConfig();
            }
            catch (JsonSerializationException ex)
            {
                HandleLoadFail(ex.Message, configText);
            }
            catch (JsonReaderException ex)
            {
                HandleLoadFail(ex.Message, configText);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
        }

        private void CopyToOldConfig()
        {
            string configStr = JsonConvert.SerializeObject(config);
            oldConfig = JsonConvert.DeserializeObject<ConfigModel>(configStr);
        }

        private void HandleLoadFail(string err, string configText)
        {
            //  创建基础配置
            CreateDefaultConfig();

            Save();

            Logger.Error(err + "\r\nConfig content: " + configText);
        }
        private void CreateDefaultConfig()
        {
            config = new ConfigModel();
            config.Links = new List<Models.Config.Link.LinkModel>();

            config.General = new GeneralModel();
            config.General.IsStartatboot = false;

            config.Behavior = new BehaviorModel();
        }

        public ConfigModel GetConfig()
        {
            return config;
        }


        public void Save()
        {
            UpdateAndSave(_ => true);
        }

        public bool UpdateAndSave(Func<ConfigModel, bool> update)
        {
            lock (saveLock)
            {
                try
                {
                    if (config == null) return false;
                    if (!update(config)) return true;
                    string dir = Path.GetDirectoryName(fileName);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    var serialized = JsonConvert.SerializeObject(config);
                    var temporaryFile = fileName + ".tmp";
                    File.WriteAllText(temporaryFile, serialized);
                    File.Move(temporaryFile, fileName, true);

                    var previous = oldConfig ?? config;
                    oldConfig = JsonConvert.DeserializeObject<ConfigModel>(serialized);
                    try
                    {
                        ConfigChanged?.Invoke(previous, config);
                    }
                    catch (Exception exception)
                    {
                        Logger.Error("配置变更处理失败：" + exception);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message);
                    return false;
                }
            }
        }

        private void CheckOption(object obj)
        {
            System.Reflection.PropertyInfo[] properties = obj.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

            foreach (System.Reflection.PropertyInfo item in properties)
            {
                string name = item.Name;
                object value = item.GetValue(obj, null);
                if (value == null)
                {
                    //配置项不存在时创建
                    Type[] types = new Type[0];
                    object[] objs = new object[0];

                    ConstructorInfo ctor = item.PropertyType.GetConstructor(types);
                    if (ctor != null)
                    {
                        object instance = ctor.Invoke(objs);
                        item.SetValue(obj, instance);
                    }
                }
            }
        }
    }
}
