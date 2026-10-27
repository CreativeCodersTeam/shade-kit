using System.Reflection;
using System.Reflection.Emit;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine;

/// <summary>
/// Proves from the IL of the engine namespace that the engine depends on no built-in profile, phase, or skill name, so
/// that profiles can be added and changed without changing the engine.
/// </summary>
public class PhaseEngineArchitectureTests
{
    private const BindingFlags AllDeclared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                             BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(x => (OpCode)x.GetValue(null)!)
        .ToDictionary(x => x.Value);

    // The common prefix of every phase skill name, for example "shade-kit-" of "shade-kit-refine".
    private static readonly string SkillPrefix = WorkflowSkillNames.ForPhase("x")[..^1];

    private static readonly Type[] ForbiddenTypes =
    [
        typeof(WorkflowPhaseNames), typeof(WorkflowProfileNames), typeof(WorkflowSkillNames),
        typeof(BuiltInWorkflowProfiles), typeof(BuiltInWorkflowProfileCatalog), typeof(IWorkflowProfileCatalog)
    ];

    [Fact]
    public void EngineNamespace_IL_ReferencesNoMemberOfProfileNamesCatalogOrBuiltInProfiles()
    {
        // Act
        var forbiddenReferences = GetEngineInstructions()
            .Where(x => x.Instruction.OpCode.OperandType is OperandType.InlineMethod or OperandType.InlineField
                or OperandType.InlineType or OperandType.InlineTok)
            .Select(x => (x.Method, Member: ResolveMember(x.Method, x.Instruction.Token)))
            .Where(x => ForbiddenTypes.Contains(x.Member as Type ?? x.Member.DeclaringType))
            .Select(x => $"{x.Method.DeclaringType!.Name}.{x.Method.Name} -> {x.Member.Name}");

        // Assert
        forbiddenReferences
            .Should().BeEmpty("the engine gets every phase and profile from the WorkflowProfile it is given");
    }

    [Fact]
    public void EngineNamespace_IL_ContainsNoBuiltInProfilePhaseOrSkillNameLiteral()
    {
        // Arrange
        var forbiddenNames = GetBuiltInNames();

        // Act
        var forbiddenLiterals = GetEngineLiterals()
            .Where(x => forbiddenNames.Contains(x.Literal) ||
                        x.Literal.StartsWith(SkillPrefix, StringComparison.Ordinal))
            .Select(x => $"{x.Method.DeclaringType!.Name}.{x.Method.Name}: \"{x.Literal}\"");

        // Assert
        forbiddenNames
            .Should().Contain([WorkflowPhaseNames.New, WorkflowProfileNames.Quick, WorkflowSkillNames.Report]);

        forbiddenLiterals
            .Should().BeEmpty("the engine must not know any profile, phase, or skill by name");
    }

    [Fact]
    public void EngineNamespace_IL_ScannerFindsKnownLiteral()
    {
        // Act
        var literals = GetEngineLiterals().Select(x => x.Literal);

        // Assert
        literals
            .Should().Contain(PhaseEngine.ApprovalOffNote, "the scanner must see the literals of the engine");
    }

    private static HashSet<string> GetBuiltInNames()
    {
        var constants = new[] { typeof(WorkflowPhaseNames), typeof(WorkflowProfileNames), typeof(WorkflowSkillNames) }
            .SelectMany(x => x.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(x => x.IsLiteral && x.FieldType == typeof(string))
            .Select(x => (string)x.GetRawConstantValue()!);

        var definitions = BuiltInWorkflowProfiles.All.SelectMany(profile => profile.Phases
            .SelectMany(phase => phase.GeneralSkills.Append(phase.Name).Append(phase.PhaseSkill))
            .Append(profile.Name));

        return [.. constants.Concat(definitions)];
    }

    private static IEnumerable<(MethodBase Method, string Literal)> GetEngineLiterals()
    {
        return GetEngineInstructions()
            .Where(x => x.Instruction.OpCode.OperandType == OperandType.InlineString)
            .Select(x => (x.Method, x.Method.Module.ResolveString(x.Instruction.Token)));
    }

    private static IEnumerable<(MethodBase Method, (OpCode OpCode, int Token) Instruction)> GetEngineInstructions()
    {
        var engineNamespace = typeof(PhaseEngine).Namespace!;

        var methods = typeof(PhaseEngine).Assembly.GetTypes()
            .Where(x => x.Namespace is { } ns &&
                        (ns == engineNamespace || ns.StartsWith(engineNamespace + ".", StringComparison.Ordinal)))
            .SelectMany(x => x.GetMethods(AllDeclared).Cast<MethodBase>().Concat(x.GetConstructors(AllDeclared)));

        return methods.SelectMany(method => ReadInstructions(method.GetMethodBody()?.GetILAsByteArray() ?? [])
            .Select(instruction => (method, instruction)));
    }

    private static MemberInfo ResolveMember(MethodBase method, int token)
    {
        var typeArguments = method.DeclaringType is { IsGenericType: true } type ? type.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;

        return method.Module.ResolveMember(token, typeArguments, methodArguments)!;
    }

    private static List<(OpCode OpCode, int Token)> ReadInstructions(byte[] il)
    {
        var instructions = new List<(OpCode, int)>();
        var position = 0;

        while (position < il.Length)
        {
            short value = il[position++];

            if (value == 0xFE)
            {
                value = unchecked((short)(0xFE00 | il[position++]));
            }

            var opCode = OpCodesByValue[value];
            var operandSize = GetOperandSize(opCode.OperandType, il, position);
            var token = operandSize == 4 ? BitConverter.ToInt32(il, position) : 0;

            instructions.Add((opCode, token));
            position += operandSize;
        }

        return instructions;
    }

    private static int GetOperandSize(OperandType operandType, byte[] il, int position)
    {
        return operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, position)),
            _ => 4
        };
    }
}
