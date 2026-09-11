// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Language.Compiler
{
    /// <summary>
    /// Immutable typed semantic parameter.
    /// Factory methods ensure that only the slot selected by ValueType is
    /// populated. JSON transport DTOs should remain separate from this
    /// runtime domain contract.
    /// </summary>
    public sealed class SemanticParameter
    {
        public string ParameterId { get; }
        public SemanticParameterValueType ValueType { get; }
        public string StringValue { get; }
        public int IntValue { get; }
        public float FloatValue { get; }
        public bool BoolValue { get; }
        public string EnumValue { get; }
        public string Unit { get; }
        public bool IsSpecified { get; }

        private SemanticParameter(
            string parameterId,
            SemanticParameterValueType valueType,
            string stringValue,
            int intValue,
            float floatValue,
            bool boolValue,
            string enumValue,
            string unit,
            bool isSpecified)
        {
            ParameterId = SccContractUtility.Text(parameterId);
            ValueType = valueType;
            StringValue = SccContractUtility.Text(stringValue);
            IntValue = intValue;
            FloatValue = floatValue;
            BoolValue = boolValue;
            EnumValue = SccContractUtility.Text(enumValue);
            Unit = SccContractUtility.Text(unit);
            IsSpecified = isSpecified;
        }

        public static SemanticParameter Unspecified(
            string parameterId,
            string unit = "")
        {
            return new SemanticParameter(
                parameterId,
                SemanticParameterValueType.Unspecified,
                string.Empty,
                0,
                0f,
                false,
                string.Empty,
                unit,
                false
            );
        }

        public static SemanticParameter FromString(
            string parameterId,
            string value,
            string unit = "")
        {
            return new SemanticParameter(
                parameterId,
                SemanticParameterValueType.String,
                value,
                0,
                0f,
                false,
                string.Empty,
                unit,
                true
            );
        }

        public static SemanticParameter FromInteger(
            string parameterId,
            int value,
            string unit = "")
        {
            return new SemanticParameter(
                parameterId,
                SemanticParameterValueType.Integer,
                string.Empty,
                value,
                0f,
                false,
                string.Empty,
                unit,
                true
            );
        }

        public static SemanticParameter FromFloat(
            string parameterId,
            float value,
            string unit = "")
        {
            return new SemanticParameter(
                parameterId,
                SemanticParameterValueType.Float,
                string.Empty,
                0,
                value,
                false,
                string.Empty,
                unit,
                true
            );
        }

        public static SemanticParameter FromBoolean(
            string parameterId,
            bool value,
            string unit = "")
        {
            return new SemanticParameter(
                parameterId,
                SemanticParameterValueType.Boolean,
                string.Empty,
                0,
                0f,
                value,
                string.Empty,
                unit,
                true
            );
        }

        public static SemanticParameter FromEnum(
            string parameterId,
            string value,
            string unit = "")
        {
            return new SemanticParameter(
                parameterId,
                SemanticParameterValueType.Enum,
                string.Empty,
                0,
                0f,
                false,
                value,
                unit,
                true
            );
        }
    }
}
