﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.AspNetCore.OData.Common;
using Microsoft.AspNetCore.OData.Edm;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.Routing;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;

namespace Microsoft.AspNetCore.OData.Routing.Template
{
    /// <summary>
    /// Helper methods for segment template
    /// </summary>
    internal static class SegmentTemplateHelpers
    {
        /// <summary>
        /// Match the function parameter
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="function">The Edm function.</param>
        /// <param name="parameterMappings">The parameter mapping.</param>
        /// <returns></returns>
        public static IList<OperationSegmentParameter> Match(ODataTemplateTranslateContext context,
            IEdmFunction function,
            IDictionary<string, string> parameterMappings)
        {
            Contract.Assert(context != null);
            Contract.Assert(function != null);
            Contract.Assert(parameterMappings != null);

            RouteValueDictionary routeValues = context.RouteValues;
            RouteValueDictionary updatedValues = context.UpdatedValues;

            IList<OperationSegmentParameter> parameters = new List<OperationSegmentParameter>();
            foreach (var parameter in parameterMappings)
            {
                string parameterName = parameter.Key;
                string parameterTemp = parameter.Value;

                IEdmOperationParameter edmParameter = function.Parameters.FirstOrDefault(p => p.Name == parameterName);
                Contract.Assert(edmParameter != null);

                // For a parameter mapping like: minSalary={min}
                // and a request like: ~/MyFunction(minSalary=2)
                // the routeValue includes the [min=2], so we should use the mapping name to retrieve the value.
                if (routeValues.TryGetValue(parameterTemp, out object rawValue))
                {
                    string strValue = rawValue as string;
                    string newStrValue = context.GetParameterAliasOrSelf(strValue);
                    if (newStrValue != strValue)
                    {
                        updatedValues[parameterTemp] = newStrValue;
                        strValue = newStrValue;
                    }

                    // for resource or collection resource, this method will return "ODataResourceValue, ..." we should support it.
                    if (edmParameter.Type.IsResourceOrCollectionResource())
                    {
                        // For FromODataUri
                        string prefixName = ODataParameterValue.ParameterValuePrefix + parameterTemp;
                        updatedValues[prefixName] = new ODataParameterValue(strValue, edmParameter.Type);

                        parameters.Add(new OperationSegmentParameter(parameterName, strValue));
                    }
                    else
                    {
                        if (edmParameter.Type.IsEnum() && strValue.StartsWith("'", StringComparison.Ordinal) && strValue.EndsWith("'", StringComparison.Ordinal))
                        {
                            // related implementation at: https://github.com/OData/odata.net/blob/master/src/Microsoft.OData.Core/UriParser/Resolver/StringAsEnumResolver.cs#L131
                            strValue = edmParameter.Type.FullName() + strValue;
                        }

                        object newValue = ODataUriUtils.ConvertFromUriLiteral(strValue, ODataVersion.V4, context.Model, edmParameter.Type);

                        // for without FromODataUri, so update it, for example, remove the single quote for string value.
                        updatedValues[parameterTemp] = newValue;

                        // For FromODataUri
                        string prefixName = ODataParameterValue.ParameterValuePrefix + parameterTemp;
                        updatedValues[prefixName] = new ODataParameterValue(newValue, edmParameter.Type);

                        parameters.Add(new OperationSegmentParameter(parameterName, newValue));
                    }
                }
                else
                {
                    return null;
                }
            }

            return parameters;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="routeValues"></param>
        /// <param name="parameterMappings"></param>
        /// <returns></returns>
        internal static bool IsMatchParameters(RouteValueDictionary routeValues, IDictionary<string, string> parameterMappings)
        {
            Contract.Assert(routeValues != null);
            Contract.Assert(parameterMappings != null);

            // If we have a function(p1, p2, p3), where p3 is optinal parameter.
            // In controller, we may have two functions:
            // IActionResult function(p1, p2)   --> #1
            // IActionResult function(p1, p2, p3)  --> #2
            // #1  can match request like: ~/function(p1=a, p2=b) , where p1=a, p2=b   (----a)
            // It also match request like: ~/function(p1=a,p2=b,p3=c), where p2="b,p3=c".  (----b)
            // However, b request should match the #2 method and skip the #1 method.
            // Here is a workaround:
            // 1) We get all the parameters from the function and all parameter values from routeValue.
            // Combine them as a string. so, actualParameters = "p1=a,p2=b,p3=c"

            IDictionary<string, string>  actualParameters = new Dictionary<string, string>();
            foreach (var parameter in parameterMappings)
            {
                // For a parameter mapping like: minSalary={min}
                // and a request like: ~/MyFunction(minSalary=2)
                // the routeValue includes the [min=2], so we should use the mapping name to retrieve the value.
                string parameterTemp = parameter.Value;
                if (routeValues.TryGetValue(parameterTemp, out object rawValue))
                {
                    actualParameters[parameterTemp] = rawValue as string;
                }
            }

            if (!actualParameters.Any())
            {
                if (parameterMappings.Any())
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }

            string combintes = string.Join(",", actualParameters.Select(kvp => kvp.Key + "=" + kvp.Value));

            // 2) Extract the key/value pairs
            //   p1=a    p2=b    p3=c
            if (!KeyValuePairParser.TryParse(combintes, out IDictionary<string, string> parsedKeyValues))
            {
                return false;
            }

            // 3) now the RequiredParameters (p1, p3) is not equal to actualParameters (p1, p2, p3)
            return parameterMappings.Count == actualParameters.Keys.Count;
        }
    }
}
