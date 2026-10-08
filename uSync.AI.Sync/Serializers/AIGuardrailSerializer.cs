using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Umbraco.AI.Core.Guardrails;
using uSync.AI.Sync.Services;
using uSync.Core;
using uSync.Core.Extensions;
using uSync.Core.Models;
using uSync.Core.Serialization;

namespace uSync.AI.Sync.Serializers;

/// <summary>
/// Serializes an Umbraco.AI <see cref="AIGuardrail"/> and its rules. Rules are written one
/// element each, in sort order, with the evaluator's own config as JSON.
/// </summary>
[SyncSerializer("7F0B2C7B-6C1D-4E0F-9B3A-5D2E8A1C4F02", "AI Guardrail Serializer", "AIGuardrail")]
public class AIGuardrailSerializer : SyncSerializerRoot<AIGuardrail>, ISyncSerializer<AIGuardrail>
{
    private readonly SyncAIService _aiService;

    public AIGuardrailSerializer(ILogger<SyncSerializerRoot<AIGuardrail>> logger, SyncAIService aiService)
        : base(logger)
    {
        _aiService = aiService;
    }

    protected override Task<SyncAttempt<XElement>> SerializeCoreAsync(AIGuardrail item, SyncSerializerOptions options)
    {
        var node = InitializeBaseNode(item, ItemAlias(item));

        node.Add(new XElement("Info", new XElement("Name", item.Name)));

        var rules = new XElement("Rules");
        foreach (var rule in item.Rules.OrderBy(x => x.SortOrder).ThenBy(x => x.Name, StringComparer.Ordinal))
        {
            var ruleNode = new XElement("Rule",
                new XAttribute("Key", rule.Id),
                new XElement("Name", rule.Name),
                new XElement("EvaluatorId", rule.EvaluatorId),
                new XElement("Phase", rule.Phase),
                new XElement("Action", rule.Action),
                new XElement("SortOrder", rule.SortOrder));

            // GuardrailId/GuardrailName are back-references to the owner, filled in by
            // Umbraco.AI when it resolves rules - not part of the rule's own definition.
            if (SyncAIJson.ToElement("Config", rule.Config) is { } config) ruleNode.Add(config);

            rules.Add(ruleNode);
        }

        node.Add(rules);

        return Task.FromResult(SyncAttempt<XElement>.Succeed(item.Name, node, ChangeType.Export, []));
    }

    protected override async Task<SyncAttempt<AIGuardrail>> DeserializeCoreAsync(XElement node, SyncSerializerOptions options)
    {
        var alias = node.GetAlias();
        var key = node.GetKey();
        var name = node.Element("Info")?.Element("Name").ValueOrDefault(alias) ?? alias;

        var item = await FindItemAsync(key) ?? await FindItemAsync(alias)
            ?? new AIGuardrail { Alias = alias, Name = name }.WithId(key);

        item.Alias = alias;
        item.Name = name;

        var rules = new List<AIGuardrailRule>();
        foreach (var ruleNode in node.Element("Rules")?.Elements("Rule") ?? [])
        {
            var rule = new AIGuardrailRule
            {
                Name = ruleNode.Element("Name").ValueOrDefault(string.Empty),
                EvaluatorId = ruleNode.Element("EvaluatorId").ValueOrDefault(string.Empty),
                Phase = ruleNode.Element("Phase").ValueOrDefault(AIGuardrailPhase.PostGenerate),
                Action = ruleNode.Element("Action").ValueOrDefault(AIGuardrailAction.Block),
                SortOrder = ruleNode.Element("SortOrder").ValueOrDefault(rules.Count),
                Config = SyncAIJson.ToJsonElement(ruleNode.Element("Config")),
            };

            var ruleKey = ruleNode.Attribute("Key").ValueOrDefault(Guid.Empty);
            if (ruleKey != Guid.Empty) rule.WithId(ruleKey);

            rules.Add(rule);
        }

        item.Rules = rules;

        return SyncAttempt<AIGuardrail>.Succeed(item.Name, item, ChangeType.Import, []);
    }

    public override Task<AIGuardrail?> FindItemAsync(Guid key) => _aiService.GetGuardrailAsync(key);

    public override Task<AIGuardrail?> FindItemAsync(string alias) => _aiService.GetGuardrailAsync(alias);

    public override Task SaveItemAsync(AIGuardrail item) => _aiService.SaveGuardrailAsync(item);

    public override Task DeleteItemAsync(AIGuardrail item) => _aiService.DeleteGuardrailAsync(item);

    public override string ItemAlias(AIGuardrail item) => item.Alias;

    public override Guid ItemKey(AIGuardrail item) => item.Id;
}
