using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

// 어셈블리 경계의 규칙(docs/COMBAT_ARCHITECTURE.md 13절)을 컴파일된 어셈블리로 확인한다.
// - 참조 방향: Core ← Simulation ← Game ← View ← UI ← App. Core·Simulation은 Unity를 참조하지 않는다.
//   asmdef가 강제하지만 asmdef는 손으로 고칠 수 있어서, 실제로 컴파일된 참조(쓰는 것만 남는다)를 본다.
// - 읽기 전용 접점(13-6, D52): 바깥(View, UI, App, DevTools)은 전투를 진행하는 구체 형식을 필드·속성·인자·반환형·사건으로 쥐지 않는다.
//   메서드 안의 형 변환은 보지 못한다(리뷰 몫).
// - 화면이 없는 층(Core, Simulation, Game)에는 MonoBehaviour 같은 Unity 컴포넌트가 없다.
[TestFixture]
public class ArchitectureTests
{
    private const string Prefix = "IdleGameProtoType.";

    private static readonly Assembly Core = typeof(GameNumber).Assembly;
    private static readonly Assembly Simulation = typeof(Combatant).Assembly;
    private static readonly Assembly GameLayer = typeof(Game).Assembly;
    private static readonly Assembly View = typeof(BattleView).Assembly;
    private static readonly Assembly UI = typeof(UIRoot).Assembly;
    private static readonly Assembly App = typeof(GameInitializer).Assembly;
    private static readonly Assembly Tools = typeof(DevTools).Assembly;

    // 전투를 만들고 진행하는 구체 형식. 주인(Game)과 시뮬레이션만 쥔다.
    private static readonly Type[] OwnerOnly =
    {
        typeof(Combatant), typeof(CombatWorld), typeof(EncounterDirector), typeof(SimulationLoop), typeof(SimClock),
        typeof(SkillLoadout), typeof(Skill), typeof(CombatActions), typeof(ActionRunner), typeof(CombatScheduler), typeof(EnemyRoster),
    };

    [Test]
    public void Assemblies_AreWhereTheLayersSayTheyAre()
    {
        string[] expected = { "Core", "Simulation", "Game", "View", "UI", "App", "DevTools" };
        Assembly[] actual = { Core, Simulation, GameLayer, View, UI, App, Tools };
        for (int i = 0; i < expected.Length; i++) Assert.AreEqual(Prefix + expected[i], actual[i].GetName().Name);
    }

    [Test]
    public void References_GoOneWay_AndCoreAndSimulationHaveNoEngine()
    {
        var allowed = new Dictionary<Assembly, string[]>
        {
            [Core] = new string[0],
            [Simulation] = new[] { "Core" },
            [GameLayer] = new[] { "Core", "Simulation" },
            [View] = new[] { "Core", "Simulation", "Game" },
            [UI] = new[] { "Core", "Simulation", "Game", "View" },
            [App] = new[] { "Core", "Simulation", "Game", "View", "UI" },
        };

        foreach (KeyValuePair<Assembly, string[]> pair in allowed)
        {
            string name = pair.Key.GetName().Name;
            foreach (AssemblyName reference in pair.Key.GetReferencedAssemblies())
            {
                if (!reference.Name.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                string layer = reference.Name.Substring(Prefix.Length);
                Assert.Contains(layer, pair.Value, $"{name}이(가) {reference.Name}을(를) 참조한다(한 방향 위반)");
            }
        }

        foreach (Assembly engineFree in new[] { Core, Simulation })
        {
            string[] engine = engineFree.GetReferencedAssemblies().Select(r => r.Name)
                .Where(n => n.StartsWith("UnityEngine", StringComparison.Ordinal) || n.StartsWith("UnityEditor", StringComparison.Ordinal)).ToArray();
            CollectionAssert.IsEmpty(engine, $"{engineFree.GetName().Name}이(가) Unity를 참조한다");
        }
    }

    [Test]
    public void LayersWithoutScreens_HaveNoUnityComponents()
    {
        foreach (Assembly assembly in new[] { Core, Simulation, GameLayer })
        {
            string[] components = assembly.GetTypes().Where(t => typeof(UnityEngine.Component).IsAssignableFrom(t)).Select(t => t.FullName).ToArray();
            CollectionAssert.IsEmpty(components, $"{assembly.GetName().Name}에 Unity 컴포넌트가 있다");
        }
    }

    [Test]
    public void Observers_HoldOnlyReadOnlySurfaces()
    {
        var violations = new List<string>();
        foreach (Assembly assembly in new[] { View, UI, App, Tools })
        {
            foreach (Type type in assembly.GetTypes()) CollectViolations(type, violations);
        }

        CollectionAssert.IsEmpty(violations, "바깥은 읽기 전용 접점(IReadOnlyCombatWorld, IEncounterFlow, IReadOnlyCombatant)만 쥔다:\n" + string.Join("\n", violations));
    }

    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static void CollectViolations(Type type, List<string> into)
    {
        void Check(Type used, string where)
        {
            Type owner = FindOwnerOnly(used);
            if (owner != null) into.Add($"{type.FullName}.{where}: {owner.Name}");
        }

        foreach (FieldInfo field in type.GetFields(Declared)) Check(field.FieldType, field.Name);
        foreach (PropertyInfo property in type.GetProperties(Declared)) Check(property.PropertyType, property.Name);
        foreach (EventInfo e in type.GetEvents(Declared)) Check(e.EventHandlerType, e.Name);
        foreach (MethodInfo method in type.GetMethods(Declared))
        {
            Check(method.ReturnType, method.Name + " 반환");
            foreach (ParameterInfo parameter in method.GetParameters()) Check(parameter.ParameterType, $"{method.Name}({parameter.Name})");
        }
        foreach (ConstructorInfo constructor in type.GetConstructors(Declared))
        {
            foreach (ParameterInfo parameter in constructor.GetParameters()) Check(parameter.ParameterType, $"생성자({parameter.Name})");
        }
    }

    // 형식 안(배열, 참조, 제네릭 인자)까지 본다.
    private static Type FindOwnerOnly(Type type)
    {
        if (type == null) return null;
        if (type.HasElementType) return FindOwnerOnly(type.GetElementType());
        if (Array.IndexOf(OwnerOnly, type) >= 0) return type;
        if (type.IsGenericType)
        {
            foreach (Type argument in type.GetGenericArguments())
            {
                Type found = FindOwnerOnly(argument);
                if (found != null) return found;
            }
        }
        return null;
    }
}
