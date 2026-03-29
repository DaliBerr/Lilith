using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace Kernel.MapGrid
{
    [Serializable]
    public sealed class MapGridCoordinateBinding
    {
        private const BindingFlags InstanceMemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Dictionary<string, Type> TypeCache = new(StringComparer.Ordinal);

        [SerializeField] private string componentTypeName = string.Empty;
        [SerializeField] private string xMemberName = string.Empty;
        [SerializeField] private string yMemberName = string.Empty;
        [SerializeField] private string setCoordinatesMethodName = string.Empty;
        [SerializeField] private string getCoordinatesMethodName = string.Empty;

        public string ComponentTypeName
        {
            get => componentTypeName;
            set => componentTypeName = value ?? string.Empty;
        }

        public string XMemberName
        {
            get => xMemberName;
            set => xMemberName = value ?? string.Empty;
        }

        public string YMemberName
        {
            get => yMemberName;
            set => yMemberName = value ?? string.Empty;
        }

        public string SetCoordinatesMethodName
        {
            get => setCoordinatesMethodName;
            set => setCoordinatesMethodName = value ?? string.Empty;
        }

        public string GetCoordinatesMethodName
        {
            get => getCoordinatesMethodName;
            set => getCoordinatesMethodName = value ?? string.Empty;
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(componentTypeName) &&
            HasWritableBinding() &&
            HasReadableBinding();

        public bool TryResolveComponentType(out Type componentType, out string error)
        {
            componentType = null;
            error = null;

            if (string.IsNullOrWhiteSpace(componentTypeName))
            {
                error = "Coordinate binding is missing Component Type Name.";
                return false;
            }

            if (TypeCache.TryGetValue(componentTypeName, out componentType) && componentType != null)
            {
                return true;
            }

            componentType = Type.GetType(componentTypeName, throwOnError: false);
            if (componentType == null)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    componentType = assembly.GetType(componentTypeName, throwOnError: false);
                    if (componentType != null)
                    {
                        break;
                    }

                    Type[] candidateTypes;
                    try
                    {
                        candidateTypes = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException exception)
                    {
                        candidateTypes = exception.Types;
                    }

                    foreach (var candidateType in candidateTypes)
                    {
                        if (candidateType == null)
                        {
                            continue;
                        }

                        if (string.Equals(candidateType.Name, componentTypeName, StringComparison.Ordinal))
                        {
                            componentType = candidateType;
                            break;
                        }
                    }

                    if (componentType != null)
                    {
                        break;
                    }
                }
            }

            if (componentType == null)
            {
                error = $"Unable to resolve coordinate component type '{componentTypeName}'.";
                return false;
            }

            TypeCache[componentTypeName] = componentType;
            return true;
        }

        public bool TryResolveComponent(GameObject target, out Component component, out Type componentType, out string error)
        {
            component = null;
            componentType = null;
            error = null;

            if (target == null)
            {
                error = "Target GameObject is null.";
                return false;
            }

            if (!TryResolveComponentType(out componentType, out error))
            {
                return false;
            }

            component = target.GetComponent(componentType);
            if (component == null)
            {
                error = $"GameObject '{target.name}' is missing coordinate component '{componentType.Name}'.";
                return false;
            }

            return true;
        }

        public bool HasCoordinateComponent(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            if (!TryResolveComponentType(out var componentType, out _))
            {
                return false;
            }

            return target.GetComponent(componentType) != null;
        }

        public bool TryValidate(GameObject target, bool requireRead, bool requireWrite, out string error)
        {
            error = null;

            if (!TryResolveComponent(target, out _, out var componentType, out error))
            {
                return false;
            }

            if (requireWrite)
            {
                if (!HasWritableBinding())
                {
                    error = "Coordinate binding is missing writable members or a writable setter method.";
                    return false;
                }

                if (HasSetMethodBinding())
                {
                    if (!TryFindWritableMethod(componentType, out _, out error))
                    {
                        return false;
                    }
                }
                else if (!TryFindWritableMembers(componentType, out _, out _, out error))
                {
                    return false;
                }
            }

            if (requireRead)
            {
                if (!HasReadableBinding())
                {
                    error = "Coordinate binding is missing readable members or a readable getter method.";
                    return false;
                }

                if (HasGetMethodBinding())
                {
                    if (!TryFindReadableMethod(componentType, out _, out error))
                    {
                        return false;
                    }
                }
                else if (!TryFindReadableMembers(componentType, out _, out _, out error))
                {
                    return false;
                }
            }

            return true;
        }

        public bool TrySetCoordinates(GameObject target, int x, int y, out string error)
        {
            error = null;

            if (!TryResolveComponent(target, out var component, out var componentType, out error))
            {
                return false;
            }

            if (HasSetMethodBinding())
            {
                if (!TryFindWritableMethod(componentType, out var methodInfo, out error))
                {
                    return false;
                }

                var parameters = methodInfo.GetParameters();
                var arguments = parameters.Length == 1
                    ? new object[] { new Vector2Int(x, y) }
                    : new object[] { x, y };
                methodInfo.Invoke(component, arguments);
                return true;
            }

            if (!TryFindWritableMembers(componentType, out var xMember, out var yMember, out error))
            {
                return false;
            }

            return TryAssignIntMember(component, xMember, x, out error) &&
                   TryAssignIntMember(component, yMember, y, out error);
        }

        public bool TryGetCoordinates(GameObject target, out Vector2Int coordinates, out string error)
        {
            coordinates = default;
            error = null;

            if (!TryResolveComponent(target, out var component, out var componentType, out error))
            {
                return false;
            }

            if (HasGetMethodBinding())
            {
                if (!TryFindReadableMethod(componentType, out var methodInfo, out error))
                {
                    return false;
                }

                var value = methodInfo.Invoke(component, Array.Empty<object>());
                if (value is Vector2Int vector)
                {
                    coordinates = vector;
                    return true;
                }

                error = $"Readable method '{getCoordinatesMethodName}' on '{componentType.Name}' must return Vector2Int.";
                return false;
            }

            if (!TryFindReadableMembers(componentType, out var xMember, out var yMember, out error))
            {
                return false;
            }

            if (!TryReadIntMember(component, xMember, out var x, out error) ||
                !TryReadIntMember(component, yMember, out var y, out error))
            {
                return false;
            }

            coordinates = new Vector2Int(x, y);
            return true;
        }

        private bool HasWritableBinding()
        {
            return HasSetMethodBinding() || HasMemberBinding();
        }

        private bool HasReadableBinding()
        {
            return HasGetMethodBinding() || HasMemberBinding();
        }

        private bool HasMemberBinding()
        {
            return !string.IsNullOrWhiteSpace(xMemberName) && !string.IsNullOrWhiteSpace(yMemberName);
        }

        private bool HasSetMethodBinding()
        {
            return !string.IsNullOrWhiteSpace(setCoordinatesMethodName);
        }

        private bool HasGetMethodBinding()
        {
            return !string.IsNullOrWhiteSpace(getCoordinatesMethodName);
        }

        private bool TryFindWritableMethod(Type componentType, out MethodInfo methodInfo, out string error)
        {
            methodInfo = FindMethod(componentType, setCoordinatesMethodName, typeof(int), typeof(int));
            if (methodInfo != null)
            {
                error = null;
                return true;
            }

            methodInfo = FindMethod(componentType, setCoordinatesMethodName, typeof(Vector2Int));
            if (methodInfo != null)
            {
                error = null;
                return true;
            }

            error = $"Unable to find writable coordinate method '{setCoordinatesMethodName}' on '{componentType.Name}'.";
            return false;
        }

        private bool TryFindReadableMethod(Type componentType, out MethodInfo methodInfo, out string error)
        {
            methodInfo = FindMethod(componentType, getCoordinatesMethodName);
            if (methodInfo == null || methodInfo.ReturnType != typeof(Vector2Int) || methodInfo.GetParameters().Length != 0)
            {
                error = $"Unable to find readable coordinate method '{getCoordinatesMethodName}' on '{componentType.Name}'.";
                return false;
            }

            error = null;
            return true;
        }

        private bool TryFindWritableMembers(Type componentType, out MemberInfo xMember, out MemberInfo yMember, out string error)
        {
            return TryFindCoordinateMembers(componentType, requireWritable: true, out xMember, out yMember, out error);
        }

        private bool TryFindReadableMembers(Type componentType, out MemberInfo xMember, out MemberInfo yMember, out string error)
        {
            return TryFindCoordinateMembers(componentType, requireWritable: false, out xMember, out yMember, out error);
        }

        private bool TryFindCoordinateMembers(
            Type componentType,
            bool requireWritable,
            out MemberInfo xMember,
            out MemberInfo yMember,
            out string error)
        {
            xMember = FindMember(componentType, xMemberName);
            yMember = FindMember(componentType, yMemberName);

            if (xMember == null)
            {
                error = $"Unable to find coordinate member '{xMemberName}' on '{componentType.Name}'.";
                return false;
            }

            if (yMember == null)
            {
                error = $"Unable to find coordinate member '{yMemberName}' on '{componentType.Name}'.";
                return false;
            }

            if (!ValidateMember(xMember, requireWritable, out error) ||
                !ValidateMember(yMember, requireWritable, out error))
            {
                return false;
            }

            return true;
        }

        private static MemberInfo FindMember(Type componentType, string memberName)
        {
            if (string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            for (var current = componentType; current != null; current = current.BaseType)
            {
                var field = current.GetField(memberName, InstanceMemberFlags);
                if (field != null)
                {
                    return field;
                }

                var property = current.GetProperty(memberName, InstanceMemberFlags);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static MethodInfo FindMethod(Type componentType, string methodName, params Type[] parameterTypes)
        {
            if (string.IsNullOrWhiteSpace(methodName))
            {
                return null;
            }

            for (var current = componentType; current != null; current = current.BaseType)
            {
                var method = current.GetMethod(methodName, InstanceMemberFlags, binder: null, types: parameterTypes, modifiers: null);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }

        private static bool ValidateMember(MemberInfo memberInfo, bool requireWritable, out string error)
        {
            switch (memberInfo)
            {
                case FieldInfo fieldInfo:
                    if (!CanConvertNumeric(fieldInfo.FieldType))
                    {
                        error = $"Member '{fieldInfo.Name}' must be a numeric type.";
                        return false;
                    }

                    error = null;
                    return true;

                case PropertyInfo propertyInfo:
                    if (!CanConvertNumeric(propertyInfo.PropertyType))
                    {
                        error = $"Property '{propertyInfo.Name}' must be a numeric type.";
                        return false;
                    }

                    if (requireWritable && !propertyInfo.CanWrite)
                    {
                        error = $"Property '{propertyInfo.Name}' is not writable.";
                        return false;
                    }

                    if (!requireWritable && !propertyInfo.CanRead)
                    {
                        error = $"Property '{propertyInfo.Name}' is not readable.";
                        return false;
                    }

                    error = null;
                    return true;

                default:
                    error = $"Unsupported coordinate member type '{memberInfo.MemberType}'.";
                    return false;
            }
        }

        private static bool TryAssignIntMember(Component target, MemberInfo memberInfo, int value, out string error)
        {
            switch (memberInfo)
            {
                case FieldInfo fieldInfo:
                    if (!TryConvertIntToType(value, fieldInfo.FieldType, out var convertedValue, out error))
                    {
                        return false;
                    }

                    fieldInfo.SetValue(target, convertedValue);
                    return true;

                case PropertyInfo propertyInfo:
                    if (!propertyInfo.CanWrite)
                    {
                        error = $"Property '{propertyInfo.Name}' is not writable.";
                        return false;
                    }

                    if (!TryConvertIntToType(value, propertyInfo.PropertyType, out convertedValue, out error))
                    {
                        return false;
                    }

                    propertyInfo.SetValue(target, convertedValue);
                    return true;

                default:
                    error = $"Unsupported coordinate member type '{memberInfo.MemberType}'.";
                    return false;
            }
        }

        private static bool TryReadIntMember(Component target, MemberInfo memberInfo, out int value, out string error)
        {
            value = default;
            error = null;

            object rawValue;
            switch (memberInfo)
            {
                case FieldInfo fieldInfo:
                    rawValue = fieldInfo.GetValue(target);
                    break;

                case PropertyInfo propertyInfo:
                    if (!propertyInfo.CanRead)
                    {
                        error = $"Property '{propertyInfo.Name}' is not readable.";
                        return false;
                    }

                    rawValue = propertyInfo.GetValue(target);
                    break;

                default:
                    error = $"Unsupported coordinate member type '{memberInfo.MemberType}'.";
                    return false;
            }

            if (!TryConvertValueToInt(rawValue, out value))
            {
                error = $"Unable to convert member '{memberInfo.Name}' to int.";
                return false;
            }

            return true;
        }

        private static bool TryConvertIntToType(int value, Type targetType, out object convertedValue, out string error)
        {
            convertedValue = null;
            error = null;

            var concreteType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (!CanConvertNumeric(concreteType))
            {
                error = $"Unsupported coordinate type '{targetType.Name}'.";
                return false;
            }

            try
            {
                convertedValue = Convert.ChangeType(value, concreteType, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception exception)
            {
                error = $"Failed to convert coordinate value to '{targetType.Name}': {exception.Message}";
                return false;
            }
        }

        private static bool TryConvertValueToInt(object rawValue, out int value)
        {
            value = default;

            if (rawValue == null)
            {
                return false;
            }

            switch (rawValue)
            {
                case int intValue:
                    value = intValue;
                    return true;

                case IConvertible convertible:
                    try
                    {
                        value = convertible.ToInt32(CultureInfo.InvariantCulture);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }

                default:
                    return false;
            }
        }

        private static bool CanConvertNumeric(Type type)
        {
            if (type == null || type == typeof(bool) || type == typeof(char))
            {
                return false;
            }

            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
            if (underlyingType.IsEnum)
            {
                return false;
            }

            switch (Type.GetTypeCode(underlyingType))
            {
                case TypeCode.Byte:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;

                default:
                    return false;
            }
        }
    }
}
