using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace 黑市
{
    public class 黑市配置
    {
        [JsonProperty("购买浮动文本")] public bool 购买浮动文本 { get; set; } = true;
        [JsonProperty("购买聊天栏文本")] public bool 购买聊天栏文本 { get; set; } = false;
        [JsonProperty("兑换浮动文本")] public bool 兑换浮动文本 { get; set; } = true;
        [JsonProperty("兑换聊天栏文本")] public bool 兑换聊天栏文本 { get; set; } = false;

        [JsonProperty("每页显示行数")] public int 每页显示行数 { get; set; } = 10;
        [JsonProperty("显示列数")] public int 显示列数 { get; set; } = 1;
        [JsonProperty("兑换规则")] public List<兑换规则> 兑换规则列表 { get; set; } = new();
        [JsonProperty("商品模板")] public 商品模板 商品模板 { get; set; } = new();
        [JsonProperty("商品列表")] public List<商品简写> 商品列表 { get; set; } = new();

        [JsonIgnore]
        public List<商品> 商品完整列表 => _展开商品列表;

        private List<商品> _展开商品列表 = new List<商品>();

        public static 黑市配置 加载(out string 错误报告)
        {
            错误报告 = "";
            黑市路径.确保目录存在();
            if (!File.Exists(黑市路径.配置路径))
            {
                var 默认 = 创建默认配置();
                默认.保存();
                return 默认;
            }
            try
            {
                var json = File.ReadAllText(黑市路径.配置路径);

                // 严格验证 JSON 格式完整性
                try { JToken.Parse(json); }
                catch (Exception jsonEx)
                {
                    错误报告 = "黑市.json JSON格式错误：" + jsonEx.Message;
                    return new 黑市配置();
                }

                var 配置 = JsonConvert.DeserializeObject<黑市配置>(json);
                if (配置 == null)
                {
                    错误报告 = "黑市.json 反序列化失败，请检查JSON格式。";
                    return new 黑市配置();
                }

                bool 验证通过 = true;
                var 报告列表 = new List<string>();

                if (配置.每页显示行数 < 1)
                {
                    验证通过 = false;
                    报告列表.Add("每页显示行数不能小于1。");
                }
                if (配置.显示列数 < 1 || 配置.显示列数 > 3)
                {
                    验证通过 = false;
                    报告列表.Add("显示列数必须在1-3之间。");
                }

                if (配置.兑换规则列表 == null)
                {
                    验证通过 = false;
                    报告列表.Add("兑换规则列表不能为空。");
                }
                else
                {
                    var 兑换ID集合 = new HashSet<int>();
                    foreach (var 规则 in 配置.兑换规则列表)
                    {
                        if (规则 == null)
                        {
                            验证通过 = false;
                            报告列表.Add("发现空兑换规则。");
                            continue;
                        }
                        if (规则.来源物品ID <= 0)
                        {
                            验证通过 = false;
                            报告列表.Add("发现非法来源物品ID：" + 规则.来源物品ID + "。");
                            continue;
                        }
                        if (兑换ID集合.Contains(规则.来源物品ID))
                        {
                            验证通过 = false;
                            报告列表.Add("发现重复兑换来源物品ID：" + 规则.来源物品ID + "。");
                            continue;
                        }
                        兑换ID集合.Add(规则.来源物品ID);
                        if (规则.比例 < 1)
                        {
                            验证通过 = false;
                            报告列表.Add("兑换比例不能小于1。");
                        }
                    }
                }

                if (配置.商品模板 == null)
                {
                    验证通过 = false;
                    报告列表.Add("商品模板不能为空。");
                }
                else
                {
                    if (配置.商品模板.价格 < 0)
                    {
                        验证通过 = false;
                        报告列表.Add("商品模板价格不能为负。");
                    }
                    if (配置.商品模板.数量 < 1)
                    {
                        验证通过 = false;
                        报告列表.Add("商品模板数量不能小于1。");
                    }
                    if (string.IsNullOrWhiteSpace(配置.商品模板.货币))
                    {
                        验证通过 = false;
                        报告列表.Add("商品模板货币不能为空。");
                    }
                }

                if (配置.商品列表 == null)
                {
                    验证通过 = false;
                    报告列表.Add("商品列表不能为空。");
                }
                else
                {
                    foreach (var 简写 in 配置.商品列表)
                    {
                        if (简写 == null) continue;
                        if (简写.物品 <= 0)
                        {
                            验证通过 = false;
                            报告列表.Add("商品「" + 简写.名称 + "」物品ID非法。");
                        }
                        if (简写.价格 < 0)
                        {
                            验证通过 = false;
                            报告列表.Add("商品「" + 简写.名称 + "」价格为负。");
                        }
                    }
                }

                if (!验证通过)
                {
                    错误报告 = string.Join("\n", 报告列表);
                    return new 黑市配置();
                }

                // 验证通过，整理并保存
                配置.展开商品();
                配置.保存();
                return 配置;
            }
            catch (Exception ex)
            {
                错误报告 = "黑市.json 加载异常：" + ex.Message;
                return new 黑市配置();
            }
        }

        public static 黑市配置 加载()
        {
            return 加载(out _);
        }

        private static 黑市配置 创建默认配置()
        {
            var 默认 = new 黑市配置();
            默认.兑换规则列表.Add(new 兑换规则 { 来源物品ID = 74, 来源物品名称 = "铂金币", 目标货币 = "元宝", 比例 = 1 });

            默认.商品模板 = new 商品模板
            {
                货币 = "元宝",
                价格 = 1,
                数量 = 1,
                前缀 = 0,
                进度 = new List<string>(),
                职业 = new List<string>(),
                指令 = new List<string>(),
                盲盒 = new List<盲盒奖励>(),
                冷却 = 0
            };

            默认.商品列表.Add(new 商品简写 { 名称 = "生命水晶", 价格 = 10, 物品 = 29, 进度 = new List<string> { "肉山" } });
            默认.商品列表.Add(new 商品简写 { 名称 = "墓碑*7", 价格 = 10, 物品 = 1175, 数量 = 7 });

            默认.展开商品();
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

            File.WriteAllText(黑市路径.配置路径, sb.ToString());
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

        public 商品 获取商品(int 序号)
        {
            if (序号 < 1 || 序号 > _展开商品列表.Count) return null;
            return _展开商品列表[序号 - 1];
        }

        private void 展开商品()
        {
            _展开商品列表.Clear();
            if (商品列表 == null) return;
            foreach (var 简写 in 商品列表)
            {
                if (简写 == null) continue;
                var 完整 = new 商品
                {
                    名称 = 简写.名称,
                    物品 = 简写.物品
                };

                完整.货币 = !string.IsNullOrEmpty(简写.货币) ? 简写.货币 : (商品模板?.货币 ?? "元宝");
                完整.价格 = 简写.价格 != 0 ? 简写.价格 : (商品模板?.价格 ?? 1);
                
                // 修复：数量使用可空类型判断，明确区分"未设置"和"设置为0"
                完整.数量 = 简写.数量.HasValue ? 简写.数量.Value : (商品模板?.数量 ?? 1);
                
                完整.前缀 = 简写.前缀.HasValue ? 简写.前缀.Value : (商品模板?.前缀 ?? 0);
                完整.进度 = 简写.进度 ?? 商品模板?.进度 ?? new List<string>();
                完整.职业 = 简写.职业 ?? 商品模板?.职业 ?? new List<string>();
                完整.指令 = 简写.指令 ?? 商品模板?.指令 ?? new List<string>();
                完整.盲盒 = 简写.盲盒 ?? 商品模板?.盲盒 ?? new List<盲盒奖励>();
                完整.冷却 = 简写.冷却.HasValue ? 简写.冷却.Value : (商品模板?.冷却 ?? 0);

                _展开商品列表.Add(完整);
            }
        }
    }

    public class 商品模板
    {
        [JsonProperty("货币")] public string 货币 { get; set; } = "元宝";
        [JsonProperty("价格")] public int 价格 { get; set; } = 1;
        [JsonProperty("数量")] public int 数量 { get; set; } = 1;
        [JsonProperty("前缀")] public int 前缀 { get; set; } = 0;
        [JsonProperty("进度")] public List<string> 进度 { get; set; } = new();
        [JsonProperty("职业")] public List<string> 职业 { get; set; } = new();
        [JsonProperty("指令")] public List<string> 指令 { get; set; } = new();
        [JsonProperty("盲盒")] public List<盲盒奖励> 盲盒 { get; set; } = new();
        [JsonProperty("冷却")] public int 冷却 { get; set; } = 0;
    }

    public class 商品简写
    {
        [JsonProperty("名称")] public string 名称 { get; set; } = "";
        [JsonProperty("货币")] public string 货币 { get; set; }
        [JsonProperty("价格")] public int 价格 { get; set; }
        [JsonProperty("物品")] public int 物品 { get; set; }
        [JsonProperty("数量")] public int? 数量 { get; set; }
        [JsonProperty("前缀")] public int? 前缀 { get; set; }
        [JsonProperty("进度")] public List<string> 进度 { get; set; }
        [JsonProperty("职业")] public List<string> 职业 { get; set; }
        [JsonProperty("指令")] public List<string> 指令 { get; set; }
        [JsonProperty("盲盒")] public List<盲盒奖励> 盲盒 { get; set; }
        [JsonProperty("冷却")] public int? 冷却 { get; set; }
    }

    public class 兑换规则
    {
        [JsonProperty("来源物品ID")] public int 来源物品ID { get; set; }
        [JsonProperty("来源物品名称")] public string 来源物品名称 { get; set; } = "";
        [JsonProperty("目标货币")] public string 目标货币 { get; set; } = "";
        [JsonProperty("兑换比例（1个来源物品换多少目标货币）")] public int 比例 { get; set; } = 1;
    }

    public class 商品
    {
        [JsonProperty("名称")] public string 名称 { get; set; } = "";
        [JsonProperty("货币")] public string 货币 { get; set; } = "";
        [JsonProperty("价格")] public int 价格 { get; set; } = 1;
        [JsonProperty("物品")] public int 物品 { get; set; }
        [JsonProperty("数量")] public int 数量 { get; set; } = 1;
        [JsonProperty("前缀")] public int 前缀 { get; set; } = 0;
        [JsonProperty("进度")] public List<string> 进度 { get; set; } = new();
        [JsonProperty("职业")] public List<string> 职业 { get; set; } = new();
        [JsonProperty("指令")] public List<string> 指令 { get; set; } = new();
        [JsonProperty("盲盒")] public List<盲盒奖励> 盲盒 { get; set; } = new();
        [JsonProperty("冷却")] public int 冷却 { get; set; } = 0;
    }

    public class 盲盒奖励
    {
        [JsonProperty("物品")] public int 物品 { get; set; }
        [JsonProperty("数量")] public int 数量 { get; set; } = 1;
        [JsonProperty("权重")] public int 权重 { get; set; } = 1;
    }
}