using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace 黑市
{
    public class 任务配置
    {
        public static readonly string 配置路径 = Path.Combine(黑市路径.根目录, "任务.json");

        [JsonProperty("任务完成浮动文本")]
        public bool 任务完成浮动文本 { get; set; } = true;

        [JsonProperty("任务完成聊天栏文本")]
        public bool 任务完成聊天栏文本 { get; set; } = true;

        [JsonProperty("最大接取数量")]
        public int 最大接取数量 { get; set; } = 5;

        [JsonProperty("任务模板")]
        public 任务模板 任务模板 { get; set; } = new();

        [JsonProperty("任务列表")]
        public List<任务简写> 任务列表 { get; set; } = new();

        [JsonIgnore]
        public List<任务定义> 任务完整列表 => _展开任务列表;

        private List<任务定义> _展开任务列表 = new();

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

                if (配置.任务模板 == null)
                {
                    验证通过 = false;
                    报告列表.Add("任务模板不能为空。");
                }
                else
                {
                    if (配置.任务模板.目标数量 < 1)
                    {
                        验证通过 = false;
                        报告列表.Add("任务模板目标数量不能小于1。");
                    }
                    if (配置.任务模板.物品奖励 == null)
                        配置.任务模板.物品奖励 = new List<任务物品奖励>();
                    else
                    {
                        foreach (var 奖励 in 配置.任务模板.物品奖励)
                        {
                            if (奖励 == null) continue;
                            if (奖励.物品ID <= 0)
                            {
                                验证通过 = false;
                                报告列表.Add("任务模板物品奖励ID非法。");
                            }
                            if (奖励.数量 < 1)
                            {
                                验证通过 = false;
                                报告列表.Add("任务模板物品奖励数量不能小于1。");
                            }
                        }
                    }
                }

                if (配置.任务列表 == null)
                {
                    验证通过 = false;
                    报告列表.Add("任务列表不能为空。");
                }
                else
                {
                    var 序号集合 = new HashSet<int>();
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
                            报告列表.Add($"发现重复序号 {任务.序号}，已自动重排。");
                        }
                        序号集合.Add(任务.序号);

                        if (string.IsNullOrWhiteSpace(任务.名称))
                        {
                            验证通过 = false;
                            报告列表.Add($"序号 {任务.序号} 任务名称不能为空。");
                        }

                        int 目标数量 = 任务.目标数量 ?? 配置.任务模板?.目标数量 ?? 1;
                        if (目标数量 < 1)
                        {
                            验证通过 = false;
                            报告列表.Add($"任务「{任务.名称}」目标数量不能小于1。");
                        }

                        if (任务.货币奖励 == null)
                            任务.货币奖励 = new List<任务货币奖励>();
                        if (任务.物品奖励 == null)
                            任务.物品奖励 = new List<任务物品奖励>();
                        if (任务.执行指令 == null)
                            任务.执行指令 = new List<string>();
                        if (任务.允许职业 == null)
                            任务.允许职业 = new List<string>();

                        foreach (var 奖励 in 任务.物品奖励)
                        {
                            if (奖励 == null) continue;
                            if (奖励.物品ID <= 0)
                            {
                                验证通过 = false;
                                报告列表.Add($"任务「{任务.名称}」物品奖励ID非法。");
                            }
                            if (奖励.数量 < 1)
                            {
                                验证通过 = false;
                                报告列表.Add($"任务「{任务.名称}」物品奖励数量不能小于1。");
                            }
                        }
                    }
                }

                if (!验证通过)
                {
                    错误报告 = string.Join("\n", 报告列表);
                    return new 任务配置();
                }

                // 验证通过后：按列表顺序重新赋予连续序号（服主只需排列顺序，无需手动管理序号）
                for (int i = 0; i < 配置.任务列表.Count; i++)
                {
                    配置.任务列表[i].序号 = i + 1;
                }

                配置.展开任务();
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
            默认.任务完成浮动文本 = true;
            默认.任务完成聊天栏文本 = true;
            默认.最大接取数量 = 5;
            默认.任务模板 = new 任务模板
            {
                任务类型 = 任务类型.击杀,
                目标数量 = 1,
                进度条件 = "",
                允许职业 = new List<string>(),
                货币奖励 = new List<任务货币奖励>(),
                物品奖励 = new List<任务物品奖励>(),
                执行指令 = new List<string>()
            };
            默认.任务列表.Add(new 任务简写
            {
                序号 = 1,
                名称 = "击杀史莱姆",
                描述 = "击杀任意史莱姆累计10只",
                目标ID = 1,
                目标数量 = 10,
                货币奖励 = new List<任务货币奖励>
                {
                    new 任务货币奖励 { 货币名 = "元宝", 数量 = 100 }
                }
            });
            默认.任务列表.Add(new 任务简写
            {
                序号 = 2,
                名称 = "获取木材",
                描述 = "收集50个木材",
                目标ID = 9,
                任务类型 = 任务类型.获取物品,
                目标数量 = 50,
                物品奖励 = new List<任务物品奖励>
                {
                    new 任务物品奖励 { 物品ID = 29, 数量 = 1 }
                }
            });
            默认.展开任务();
            return 默认;
        }

        public void 保存()
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            var token = JToken.FromObject(this, JsonSerializer.Create(settings));
            var sb = new StringBuilder();
            写入格式化(token, sb, 0, false, 0);

            File.WriteAllText(配置路径, sb.ToString());
        }

        private static bool 对象应该横向(JToken token, bool 父是数组, int 深度)
        {
            if (父是数组) return true;
            if (深度 == 1 && token.Type == JTokenType.Object) return true;
            if (token.Type == JTokenType.Object && ((JObject)token).Count == 0) return true;
            return false;
        }

        private static bool 数组应该横向(JToken token, bool 父是数组, int 深度)
        {
            if (token is JArray arr && arr.Count == 0) return true;

            if (!父是数组 && token is JArray arr2 && arr2.Count <= 3)
            {
                // 嵌套在对象内部的小数组（如货币奖励、物品奖励等）一律横向
                if (深度 >= 2) return true;

                bool 全简单 = arr2.All(t =>
                    t.Type == JTokenType.String ||
                    t.Type == JTokenType.Integer ||
                    t.Type == JTokenType.Float ||
                    t.Type == JTokenType.Boolean);
                if (全简单) return true;
            }

            return false;
        }

        private static void 写入格式化(JToken token, StringBuilder sb, int indent, bool 父是数组, int 深度)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var obj = (JObject)token;
                    if (obj.Count == 0)
                    {
                        sb.Append("{}");
                        return;
                    }

                    bool 对象横向 = 对象应该横向(token, 父是数组, 深度);

                    sb.Append("{");

                    bool first = true;
                    foreach (var prop in obj.Properties())
                    {
                        if (!first)
                        {
                            sb.Append(对象横向 ? ", " : ",");
                            if (!对象横向) sb.AppendLine();
                        }
                        first = false;

                        if (!对象横向)
                            sb.Append(new string(' ', (indent + 1) * 2));

                        sb.Append("\"");
                        sb.Append(prop.Name);
                        sb.Append("\": ");

                        写入格式化(prop.Value, sb, indent + 1, false, 深度 + 1);
                    }

                    if (!对象横向)
                    {
                        sb.AppendLine();
                        sb.Append(new string(' ', indent * 2));
                    }
                    sb.Append("}");
                    break;

                case JTokenType.Array:
                    var arr = (JArray)token;
                    if (arr.Count == 0)
                    {
                        sb.Append("[]");
                        return;
                    }

                    bool 数组横向 = 数组应该横向(token, 父是数组, 深度);

                    if (数组横向)
                    {
                        sb.Append("[");
                        for (int i = 0; i < arr.Count; i++)
                        {
                            if (i > 0) sb.Append(", ");
                            写入格式化(arr[i], sb, indent, true, 深度 + 1);
                        }
                        sb.Append("]");
                    }
                    else
                    {
                        sb.AppendLine("[");
                        for (int i = 0; i < arr.Count; i++)
                        {
                            sb.Append(new string(' ', (indent + 1) * 2));
                            写入格式化(arr[i], sb, indent + 1, true, 深度 + 1);
                            if (i < arr.Count - 1) sb.Append(",");
                            sb.AppendLine();
                        }
                        sb.Append(new string(' ', indent * 2));
                        sb.Append("]");
                    }
                    break;

                case JTokenType.String:
                    sb.Append(JsonConvert.ToString(token.Value<string>()));
                    break;

                case JTokenType.Integer:
                case JTokenType.Float:
                    sb.Append(token.ToString());
                    break;

                case JTokenType.Boolean:
                    sb.Append(token.Value<bool>() ? "true" : "false");
                    break;

                case JTokenType.Null:
                    sb.Append("null");
                    break;

                default:
                    sb.Append(token.ToString());
                    break;
            }
        }

        public 任务定义 获取任务(int 序号)
        {
            return 任务完整列表.FirstOrDefault(t => t.序号 == 序号);
        }

        private void 展开任务()
        {
            _展开任务列表.Clear();
            if (任务列表 == null) return;
            foreach (var 简写 in 任务列表)
            {
                if (简写 == null) continue;
                var 完整 = new 任务定义
                {
                    序号 = 简写.序号,
                    名称 = 简写.名称,
                    描述 = 简写.描述,
                    目标ID = 简写.目标ID
                };

                完整.任务类型 = 简写.任务类型 ?? 任务模板?.任务类型 ?? 任务类型.击杀;
                完整.目标数量 = 简写.目标数量 ?? 任务模板?.目标数量 ?? 1;
                完整.进度条件 = !string.IsNullOrEmpty(简写.进度条件) ? 简写.进度条件 : (任务模板?.进度条件 ?? "");
                完整.允许职业 = 简写.允许职业 ?? 任务模板?.允许职业 ?? new List<string>();
                完整.货币奖励 = 简写.货币奖励 ?? 任务模板?.货币奖励 ?? new List<任务货币奖励>();
                完整.物品奖励 = 简写.物品奖励 ?? 任务模板?.物品奖励 ?? new List<任务物品奖励>();
                完整.执行指令 = 简写.执行指令 ?? 任务模板?.执行指令 ?? new List<string>();

                _展开任务列表.Add(完整);
            }
        }
    }

    public enum 任务类型
    {
        击杀 = 0,
        获取物品 = 1
    }

    public class 任务模板
    {
        [JsonProperty("任务类型（0=击杀,1=获取物品）")] public 任务类型 任务类型 { get; set; } = 任务类型.击杀;
        [JsonProperty("目标数量")] public int 目标数量 { get; set; } = 1;
        [JsonProperty("进度条件")] public string 进度条件 { get; set; } = "";
        [JsonProperty("允许职业")] public List<string> 允许职业 { get; set; } = new();
        [JsonProperty("货币奖励")] public List<任务货币奖励> 货币奖励 { get; set; } = new();
        [JsonProperty("物品奖励")] public List<任务物品奖励> 物品奖励 { get; set; } = new();
        [JsonProperty("执行指令")] public List<string> 执行指令 { get; set; } = new();
    }

    public class 任务简写
    {
        [JsonProperty("序号")] public int 序号 { get; set; }
        [JsonProperty("名称")] public string 名称 { get; set; } = "";
        [JsonProperty("描述")] public string 描述 { get; set; } = "";
        [JsonProperty("目标ID")] public int 目标ID { get; set; }
        [JsonProperty("任务类型（0=击杀,1=获取物品）")] public 任务类型? 任务类型 { get; set; }
        [JsonProperty("目标数量")] public int? 目标数量 { get; set; }
        [JsonProperty("进度条件")] public string 进度条件 { get; set; }
        [JsonProperty("允许职业")] public List<string> 允许职业 { get; set; }
        [JsonProperty("货币奖励")] public List<任务货币奖励> 货币奖励 { get; set; }
        [JsonProperty("物品奖励")] public List<任务物品奖励> 物品奖励 { get; set; }
        [JsonProperty("执行指令")] public List<string> 执行指令 { get; set; }
    }

    public class 任务货币奖励
    {
        [JsonProperty("货币名")] public string 货币名 { get; set; } = "";
        [JsonProperty("数量")] public int 数量 { get; set; } = 1;
    }

    public class 任务物品奖励
    {
        [JsonProperty("物品ID")] public int 物品ID { get; set; }
        [JsonProperty("数量")] public int 数量 { get; set; } = 1;
        [JsonProperty("前缀")] public int 前缀 { get; set; } = 0;
    }

    public class 任务定义
    {
        [JsonProperty("序号")] public int 序号 { get; set; }
        [JsonProperty("名称")] public string 名称 { get; set; } = "";
        [JsonProperty("描述")] public string 描述 { get; set; } = "";
        [JsonProperty("目标ID")] public int 目标ID { get; set; }
        [JsonProperty("任务类型（0=击杀,1=获取物品）")] public 任务类型 任务类型 { get; set; } = 任务类型.击杀;
        [JsonProperty("目标数量")] public int 目标数量 { get; set; } = 1;
        [JsonProperty("进度条件")] public string 进度条件 { get; set; } = "";
        [JsonProperty("允许职业")] public List<string> 允许职业 { get; set; } = new();
        [JsonProperty("货币奖励")] public List<任务货币奖励> 货币奖励 { get; set; } = new();
        [JsonProperty("物品奖励")] public List<任务物品奖励> 物品奖励 { get; set; } = new();
        [JsonProperty("执行指令")] public List<string> 执行指令 { get; set; } = new();
    }
}
