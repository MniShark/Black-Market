using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace 黑市
{
    public class 职业配置
    {
        public static readonly string 配置路径 = Path.Combine(黑市路径.根目录, "职业.json");

        [JsonProperty("职业列表")]
        public List<职业定义> 职业列表 { get; set; } = new();

        [JsonProperty("默认职业名称")]
        public string 默认职业名称 { get; set; } = "入门";

        public static 职业配置 加载(out string 错误报告)
        {
            错误报告 = "";
            黑市路径.确保目录存在();
            if (!File.Exists(配置路径))
            {
                var 默认 = 创建默认配置();
                默认.保存();
                return 默认;
            }
            try
            {
                var json = File.ReadAllText(配置路径);

                // 严格验证 JSON 格式完整性
                try { JToken.Parse(json); }
                catch (Exception jsonEx)
                {
                    错误报告 = "职业.json JSON格式错误：" + jsonEx.Message;
                    return new 职业配置();
                }

                var 配置 = JsonConvert.DeserializeObject<职业配置>(json);
                if (配置 == null)
                {
                    错误报告 = "职业.json 反序列化失败，请检查JSON格式。";
                    return new 职业配置();
                }

                bool 验证通过 = true;
                var 报告列表 = new List<string>();

                if (string.IsNullOrWhiteSpace(配置.默认职业名称))
                {
                    验证通过 = false;
                    报告列表.Add("默认职业名称不能为空。");
                }

                if (配置.职业列表 == null)
                {
                    验证通过 = false;
                    报告列表.Add("职业列表不能为空。");
                }
                else
                {
                    var 名称集合 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var 职业 in 配置.职业列表)
                    {
                        if (职业 == null)
                        {
                            验证通过 = false;
                            报告列表.Add("发现空职业条目。");
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(职业.名称))
                        {
                            验证通过 = false;
                            报告列表.Add("发现名称为空的职业。");
                            continue;
                        }
                        if (名称集合.Contains(职业.名称))
                        {
                            验证通过 = false;
                            报告列表.Add($"发现重复职业名称：{职业.名称}。");
                            continue;
                        }
                        名称集合.Add(职业.名称);

                        if (string.IsNullOrWhiteSpace(职业.颜色) || 职业.颜色.Length != 6)
                        {
                            验证通过 = false;
                            报告列表.Add($"职业「{职业.名称}」颜色格式错误，应为6位十六进制。");
                        }
                        if (职业.转职奖励物品 == null)
                            职业.转职奖励物品 = new List<转职奖励物品>();
                        if (职业.转职执行指令 == null)
                            职业.转职执行指令 = new List<string>();
                    }

                    if (!配置.职业列表.Any(j => j.名称.Equals(配置.默认职业名称, StringComparison.OrdinalIgnoreCase)))
                    {
                        验证通过 = false;
                        报告列表.Add($"默认职业「{配置.默认职业名称}」不存在于职业列表中。");
                    }
                }

                if (!验证通过)
                {
                    错误报告 = string.Join("\n", 报告列表);
                    return new 职业配置();
                }

                // 验证通过，整理并保存
                配置.保存();
                return 配置;
            }
            catch (Exception ex)
            {
                错误报告 = $"职业.json 加载异常：{ex.Message}";
                return new 职业配置();
            }
        }

        public static 职业配置 加载()
        {
            return 加载(out _);
        }

        private static 职业配置 创建默认配置()
        {
            var 默认 = new 职业配置();
            默认.职业列表.Add(new 职业定义
            {
                名称 = "入门",
                描述 = "初始职业，可转职为其他职业。",
                颜色 = "AAAAAA",
                可转职 = true
            });
            默认.职业列表.Add(new 职业定义
            {
                名称 = "战士",
                描述 = "擅长近战攻击。",
                颜色 = "FF5555",
                可转职 = false,
                转职奖励物品 = new List<转职奖励物品>(),
                转职执行指令 = new List<string>()
            });
            默认.职业列表.Add(new 职业定义
            {
                名称 = "法师",
                描述 = "擅长魔法攻击。",
                颜色 = "AA66FF",
                可转职 = false,
                转职奖励物品 = new List<转职奖励物品>
                {
                    new 转职奖励物品 { 物品ID = 3069, 数量 = 1 }
                },
                转职执行指令 = new List<string>()
            });
            默认.职业列表.Add(new 职业定义
            {
                名称 = "射手",
                描述 = "擅长远程攻击。",
                颜色 = "55FF55",
                可转职 = false,
                转职奖励物品 = new List<转职奖励物品>(),
                转职执行指令 = new List<string>()
            });
            默认.职业列表.Add(new 职业定义
            {
                名称 = "召唤",
                描述 = "擅长召唤仆从。",
                颜色 = "FF88FF",
                可转职 = false,
                转职奖励物品 = new List<转职奖励物品>
                {
                    new 转职奖励物品 { 物品ID = 5114, 数量 = 1 }
                },
                转职执行指令 = new List<string>()
            });
            return 默认;
        }

        public void 保存()
        {
            File.WriteAllText(配置路径, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public 职业定义 获取职业(string 名称)
        {
            return 职业列表.FirstOrDefault(j => j.名称.Equals(名称, StringComparison.OrdinalIgnoreCase));
        }

        public 职业定义 获取默认职业()
        {
            var 默认 = 职业列表.FirstOrDefault(j => j.名称.Equals(默认职业名称, StringComparison.OrdinalIgnoreCase));
            return 默认 ?? 职业列表.FirstOrDefault(j => j.可转职);
        }
    }

    public class 转职奖励物品
    {
        [JsonProperty("物品ID")] public int 物品ID { get; set; }
        [JsonProperty("数量")] public int 数量 { get; set; } = 1;
        [JsonProperty("前缀")] public int 前缀 { get; set; } = 0;
    }

    public class 职业定义
    {
        [JsonProperty("名称")] public string 名称 { get; set; } = "";
        [JsonProperty("描述")] public string 描述 { get; set; } = "";
        [JsonProperty("颜色")] public string 颜色 { get; set; } = "FFFFFF";
        [JsonProperty("可转职")] public bool 可转职 { get; set; } = false;
        [JsonProperty("转职奖励物品")] public List<转职奖励物品> 转职奖励物品 { get; set; } = new();
        [JsonProperty("转职执行指令")] public List<string> 转职执行指令 { get; set; } = new();
    }
}
