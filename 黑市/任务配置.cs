using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace 黑市
{
    public class 任务配置
    {
        public static readonly string 配置路径 = Path.Combine(黑市路径.根目录, "任务.json");

        [JsonProperty("最大接取数量")]
        public int 最大接取数量 { get; set; } = 5;

        [JsonProperty("任务列表")]
        public List<任务定义> 任务列表 { get; set; } = new();

        public static 任务配置 加载(out string 错误报告)
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
                    错误报告 = "任务.json JSON格式错误：" + jsonEx.Message;
                    return new 任务配置();
                }

                var 配置 = JsonConvert.DeserializeObject<任务配置>(json);
                if (配置 == null)
                {
                    错误报告 = "任务.json 反序列化失败，请检查JSON格式。";
                    return new 任务配置();
                }

                bool 验证通过 = true;
                var 报告列表 = new List<string>();

                if (配置.最大接取数量 < 1)
                {
                    验证通过 = false;
                    报告列表.Add("最大接取数量不能小于1。");
                }

                if (配置.任务列表 == null)
                {
                    验证通过 = false;
                    报告列表.Add("任务列表不能为空。");
                }
                else
                {
                    var 序号集合 = new HashSet<int>();
                    int 新序号 = 1;
                    foreach (var 任务 in 配置.任务列表)
                    {
                        if (任务 == null)
                        {
                            验证通过 = false;
                            报告列表.Add("发现空任务条目。");
                            continue;
                        }
                        if (任务.序号 <= 0)
                        {
                            验证通过 = false;
                            报告列表.Add($"任务「{任务.名称}」序号非法。");
                        }
                        if (序号集合.Contains(任务.序号))
                        {
                            验证通过 = false;
                            报告列表.Add($"发现重复序号 {任务.序号}。");
                        }
                        序号集合.Add(任务.序号);
                        新序号++;

                        if (任务.目标数量 < 1)
                        {
                            验证通过 = false;
                            报告列表.Add($"任务「{任务.名称}」目标数量不能小于1。");
                        }
                        if (任务.货币奖励 == null)
                            任务.货币奖励 = new List<任务货币奖励>();
                        if (任务.执行指令 == null)
                            任务.执行指令 = new List<string>();
                        if (任务.允许职业 == null)
                            任务.允许职业 = new List<string>();
                    }
                }

                if (!验证通过)
                {
                    错误报告 = string.Join("\n", 报告列表);
                    return new 任务配置();
                }

                // 验证通过，整理并保存
                配置.保存();
                return 配置;
            }
            catch (Exception ex)
            {
                错误报告 = $"任务.json 加载异常：{ex.Message}";
                return new 任务配置();
            }
        }

        public static 任务配置 加载()
        {
            return 加载(out _);
        }

        private static 任务配置 创建默认配置()
        {
            var 默认 = new 任务配置();
            默认.最大接取数量 = 5;
            默认.任务列表.Add(new 任务定义
            {
                序号 = 1,
                名称 = "击杀史莱姆",
                描述 = "击杀任意史莱姆累计10只",
                目标ID = 1,
                目标数量 = 10,
                进度条件 = "",
                允许职业 = new List<string>(),
                货币奖励 = new List<任务货币奖励>
                {
                    new 任务货币奖励 { 货币名 = "元宝", 数量 = 100 }
                },
                执行指令 = new List<string>()
            });
            默认.任务列表.Add(new 任务定义
            {
                序号 = 2,
                名称 = "击杀克苏鲁之眼",
                描述 = "击杀克苏鲁之眼",
                目标ID = 4,
                目标数量 = 1,
                进度条件 = "克眼",
                允许职业 = new List<string>(),
                货币奖励 = new List<任务货币奖励>(),
                执行指令 = new List<string>
                {
                    "buff {0} 1 3600"
                }
            });
            return 默认;
        }

        public void 保存()
        {
            File.WriteAllText(配置路径, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public 任务定义 获取任务(int 序号)
        {
            return 任务列表.FirstOrDefault(t => t.序号 == 序号);
        }
    }

    public class 任务货币奖励
    {
        [JsonProperty("货币名")] public string 货币名 { get; set; } = "";
        [JsonProperty("数量")] public int 数量 { get; set; } = 1;
    }

    public class 任务定义
    {
        [JsonProperty("序号")] public int 序号 { get; set; }
        [JsonProperty("名称")] public string 名称 { get; set; } = "";
        [JsonProperty("描述")] public string 描述 { get; set; } = "";
        [JsonProperty("目标ID")] public int 目标ID { get; set; }
        [JsonProperty("目标数量")] public int 目标数量 { get; set; } = 1;
        [JsonProperty("进度条件")] public string 进度条件 { get; set; } = "";
        [JsonProperty("允许职业")] public List<string> 允许职业 { get; set; } = new();
        [JsonProperty("货币奖励")] public List<任务货币奖励> 货币奖励 { get; set; } = new();
        [JsonProperty("执行指令")] public List<string> 执行指令 { get; set; } = new();
    }
}
