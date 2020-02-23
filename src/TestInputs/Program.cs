using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TestInputs
{
    [Task]
    [Task.Id("")]
    class Program
    {
        static void Main(string[] args)
        {
            Input.ToEntity(typeof(Program));
        }

        [Input]
        [Input.Label("Mola")]
        [Input.Required]
        public string Mola { get; set; }

        public enum Opcion
        {
            [Input.Label("Opcion Uno")]
            Uno,
            [Input.Label("Opcion Dos")]
            Dos,
            [Input.Label("Opcion Tres")]
            Tres
        }

        [Input]
        [Input.Label("Opciones a elegir")]
        [Input.HelpMarkDown("Las opciones posibles son Opcion Uno, Opcion Dos, Opcion Tres")]
        [Input.DefaultValue(Opcion.Uno)]
        public Opcion Opciones { get; set; }

        [Task.Variable("release")]
        public string Relase { get; set; }
    }

    internal interface IValue<T>
    {
        T Value { get; }
    }

    internal static class Extensions
    {
        public static T GetCustomAttribute<A, T>(this MemberInfo member, T defaultValue)
            where A : Attribute, IValue<T>
        {
            var attr = member.GetCustomAttribute<A>();

            return attr == null ? defaultValue : attr.Value;

        }
        public static bool In<T>(this T value, params T[] options)
        {
            return options.Contains(value);
        }

    }

    [AttributeUsage(AttributeTargets.Property)]
    public class InputAttribute : Attribute
    {
    }

    public static class Input
    {
        public enum Type { FilePath, MultiLine, Radio, SecureFile }

        [AttributeUsage(AttributeTargets.Property)]
        public class TypeAttribute : Attribute, IValue<Type>
        {
            public TypeAttribute(Type type)
            {
                Value = type;
            }
            public Type Value { get; }
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class NameAttribute : Attribute, IValue<string>
        {
            public NameAttribute(string name)
            {
                Value = name;
            }
            public string Value { get; }
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        public class LabelAttribute : Attribute, IValue<string>
        {
            public LabelAttribute(string label)
            {
                Value = label;
            }
            public string Value { get; }
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class HelpMarkDownAttribute : Attribute, IValue<string>
        {
            public HelpMarkDownAttribute(string helpMarkDown)
            {
                Value = helpMarkDown;
            }

            public HelpMarkDownAttribute(string text, string url)
            {
                Value = $"[{text}]({url})";
            }

            public string Value { get; }
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class RequiredAttribute : Attribute, IValue<bool>
        {
            public RequiredAttribute() : this(true)
            {
            }

            public RequiredAttribute(bool required)
            {
                Value = required;
            }

            public bool Value { get; }
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class DefaultValueAttribute : Attribute, IValue<object>
        {
            public DefaultValueAttribute(string defaultValue)
            {
                Value = defaultValue;
            }

            public DefaultValueAttribute(bool defaultValue)
            {
                Value = defaultValue;
            }

            public DefaultValueAttribute(object defaultValue)
            {
                Value = defaultValue;
            }

            public object Value { get; }
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class VisibleRuleAttribute : Attribute, IValue<string>
        {
            public VisibleRuleAttribute(string visibleRule)
            {
                Value = visibleRule;
            }
            public string Value { get; }
        }

        internal class InputEntity
        {
            public string name { get; set; }
            public string label { get; set; }
            public string type { get; set; }
            public object defaultValue { get; set; }
            public bool required { get; set; }
            public string helpMarkDown { get; set; }
            public string groupName { get; set; }
            public string visibleRule { get; set; }
            public object options { get; set; }
        }

        internal static InputEntity[] ToEntity(System.Type type)
        {
            return type
                .GetProperties()
                .Where(p => p.IsDefined(typeof(InputAttribute)))
                .Select(p => ToEntity(p))
                .ToArray();
        }
        internal static InputEntity ToEntity(PropertyInfo property)
        {
            var entity = new InputEntity
            {
                name = property.GetCustomAttribute<NameAttribute, string>(property.Name),
                label = property.GetCustomAttribute<LabelAttribute, string>(property.Name),
                helpMarkDown = property.GetCustomAttribute<HelpMarkDownAttribute, string>(null),
                defaultValue = property.GetCustomAttribute<DefaultValueAttribute, object>(null),
                required = property.GetCustomAttribute<RequiredAttribute, bool>(false),
                visibleRule = property.GetCustomAttribute<VisibleRuleAttribute, string>(null),
                //groupName = attributes.GetValue(Group)
            };

            var type = property.GetCustomAttribute<TypeAttribute>();

            Func<Exception> exception = () => new InvalidOperationException($"Input type '{type.Value}' not allowed for property {property.Name} of type {property.PropertyType.Name}.");

            if (property.PropertyType == typeof(string))
            {
                entity.type = "string";

                if (type != null)
                {
                    if (type.Value.In(Type.FilePath, Type.MultiLine, Type.SecureFile))
                    {
                        entity.type = type.Value.ToString();
                    }
                    else
                    {
                        throw exception();
                    }
                }
            }
            else if (property.PropertyType == typeof(bool))
            {
                entity.type = "boolean";

                if (type != null)
                {
                    if (type.Value == Type.Radio)
                    {
                        entity.type = "radio";
                    }
                    else
                    {
                        throw exception();
                    }
                }
            }
            else if (property.PropertyType.IsEnum)
            {
                entity.type = "pickList";

                if (type != null)
                {
                    throw exception();
                }

                entity.options = property.PropertyType
                    .GetFields()
                    .Where(f => f.IsLiteral)
                    .ToDictionary(x => x.Name, x => x.GetCustomAttribute<LabelAttribute, string>(x.Name));
            }

            return entity;
        }

    }

    [AttributeUsage(AttributeTargets.Class)]
    public class TaskAttribute : Attribute
    {

    }

    public static class Task
    {
        [AttributeUsage(AttributeTargets.Class)]
        public class IdAttribute : Attribute, IValue<Guid>
        {
            public IdAttribute(string guid)
            {
                Value = new Guid(guid);
            }

            public Guid Value { get; }
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class NameAttribute : Attribute, IValue<string>
        {
            readonly string pattern = "^[A-Za-z0-9\\-]+$";

            public NameAttribute(string name)
            {
                if (Regex.IsMatch(name, pattern))
                {
                    Value = name;
                }
                else
                {
                    throw new ArgumentException($"Invalid name '{name}'");
                }
            }

            public string Value { get; }
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class FriendlyNameAttribute : Attribute, IValue<string>
        {
            public FriendlyNameAttribute(string name)
            {
                if (name.Length <= 40)
                {
                    Value = name;
                }
                else
                {
                    throw new ArgumentException($"Friendly name must be <= 40 chars");
                }
            }

            public string Value { get; }
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class InstanceNameFormatAttribute : Attribute, IValue<string>
        {
            public InstanceNameFormatAttribute(string format)
            {
                Value = format;
            }

            public string Value { get; }
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class DescriptionAttribute : Attribute, IValue<string>
        {
            public DescriptionAttribute(string description)
            {
                Value = description;
            }

            public string Value { get; }
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class HelpUrlAttribute : Attribute, IValue<string>
        {
            public HelpUrlAttribute(string url)
            {
                Value = url;
            }

            public string Value { get; }
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class HelpMarkDownAttribute : Attribute, IValue<string>
        {
            public HelpMarkDownAttribute(string text)
            {
                Value = text;
            }

            public string Value { get; }
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class AuthorAttribute : Attribute, IValue<string>
        {
            public AuthorAttribute(string author)
            {
                Value = author;
            }

            public string Value { get; }
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class PreviewAttribute : Attribute, IValue<bool>
        {
            public PreviewAttribute() : this(true)
            {
            }

            public PreviewAttribute(bool preview)
            {
                Value = preview;
            }

            public bool Value { get; }
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class DeprecatedAttribute : Attribute, IValue<bool>
        {
            public DeprecatedAttribute() : this(true)
            {
            }

            public DeprecatedAttribute(bool deprecated)
            {
                Value = deprecated;
            }

            public bool Value { get; }
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class RunsOnAttribute : Attribute, IValue<RunsOn>
        {
            public RunsOnAttribute(RunsOn runsOn)
            {
                Value = runsOn;
            }

            public RunsOn Value { get; }
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class VisibilityAttribute : Attribute, IValue<Visibility>
        {
            public VisibilityAttribute(Visibility visibility)
            {
                Value = visibility;
            }

            public Visibility Value { get; }
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class CategoryAttribute : Attribute, IValue<Category>
        {
            public CategoryAttribute(Category category)
            {
                Value = category;
            }

            public Category Value { get; }
        }

        [AttributeUsage(AttributeTargets.Class)]
        public class MinimumAgentVersionAttribute : Attribute, IValue<string>
        {
            readonly string pattern = "^\\d+\\.\\d+(\\.\\d+)?$";

            public MinimumAgentVersionAttribute(string version)
            {
                if (Regex.IsMatch(version, pattern))
                {
                    Value = version;
                }
                else
                {
                    throw new ArgumentException($"Invalid version '{version}'");
                }
            }

            public string Value { get; }
        }

        [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
        public class VariableAttribute : Attribute
        {
            public VariableAttribute(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }
    }

    [Flags]
    public enum RunsOn { Agent = 1, MachineGroup = 2, Server = 4 }

    [Flags]
    public enum Visibility { Build = 1, Release = 2 }

    public enum Category { Repos, Boards, Pipelines, TestPlans, Artifacts }

}
