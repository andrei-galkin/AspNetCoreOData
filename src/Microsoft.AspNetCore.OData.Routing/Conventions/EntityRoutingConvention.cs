﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.OData.Routing.Edm;
using Microsoft.AspNetCore.OData.Routing.Template;
using Microsoft.OData.Edm;

namespace Microsoft.AspNetCore.OData.Routing.Conventions
{
    /// <summary>
    /// The convention for <see cref="IEdmEntitySet"/> with key.
    /// It supports key in parenthesis and key as segment if it's single key.
    /// </summary>
    public class EntityEndpointConvention : IODataControllerActionConvention
    {
        /// <inheritdoc />
        public int Order => 300;

        /// <inheritdoc />
        public virtual bool AppliesToController(ODataControllerActionContext context)
        {
            return context?.EntitySet != null;
        }

        /// <inheritdoc />
        public virtual bool AppliesToAction(ODataControllerActionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ActionModel action = context.Action;
            IEdmEntitySet entitySet = context.EntitySet;
            IEdmEntityType entityType = entitySet.EntityType();

            // if the action has no key parameter, skip it.
            if (!action.HasODataKeyParameter(entityType))
            {
                return false;
            }

            string actionName = action.ActionMethod.Name;

            // We care about the action in this pattern: {HttpMethod}{EntityTypeName}
            (string httpMethod, string castTypeName) = Split(actionName);
            if (httpMethod == null)
            {
                return false;
            }

            IEdmStructuredType castType = null;
            if (castTypeName != null)
            {
                castType = entityType.FindTypeInInheritance(context.Model, castTypeName);
                if (castType == null)
                {
                    return false;
                }
            }

            AddSelector(entitySet, entityType, castType, context.Prefix, context.Model, action, false);

            // if one key, we should support key as segment template
            if (entityType.Key().Count() == 1)
            {
                AddSelector(entitySet, entityType, castType, context.Prefix, context.Model, action, true);
            }

            return true;
        }

        private static (string, string) Split(string actionName)
        {
            string typeName;
            string methodName;
            if (actionName.StartsWith("Get", StringComparison.Ordinal))
            {
                typeName = actionName.Substring(3);
                methodName = "Get";
            }
            else if (actionName.StartsWith("Put", StringComparison.Ordinal))
            {
                typeName = actionName.Substring(3);
                methodName = "Put";
            }
            else if (actionName.StartsWith("Patch", StringComparison.Ordinal))
            {
                typeName = actionName.Substring(5);
                methodName = "Patch";
            }
            else if (actionName.StartsWith("Delete", StringComparison.Ordinal))
            {
                typeName = actionName.Substring(6);
                methodName = "Delete";
            }
            else
            {
                return (null, null);
            }

            if (string.IsNullOrEmpty(typeName))
            {
                return (methodName, null);
            }

            return (methodName, typeName);
        }

        private static void AddSelector(IEdmEntitySet entitySet, IEdmEntityType entityType,
            IEdmStructuredType castType, string prefix, IEdmModel model, ActionModel action, bool keyAsSegment)
        {
            IList<ODataSegmentTemplate> segments = new List<ODataSegmentTemplate>
            {
                new EntitySetSegmentTemplate(entitySet),
                new KeySegmentTemplate(entityType)
            };

            // If we have the type cast
            if (castType != null)
            {
                if (castType == entityType)
                {
                    // If cast type is the entity type of the entity set.
                    // we support two templates
                    // ~/Customers({key})
                    action.AddSelector(prefix, model, new ODataPathTemplate(segments) { KeyAsSegment = keyAsSegment });

                    // ~/Customers({key})/Ns.Customer
                    segments.Add(new CastSegmentTemplate(entityType));
                    action.AddSelector(prefix, model, new ODataPathTemplate(segments) { KeyAsSegment = keyAsSegment });
                }
                else
                {
                    // ~/Customers({key})/Ns.Customer
                    segments.Add(new CastSegmentTemplate(entityType));
                    action.AddSelector(prefix, model, new ODataPathTemplate(segments) { KeyAsSegment = keyAsSegment });
                }
            }
            else
            {
                // ~/Customers({key})
                action.AddSelector(prefix, model, new ODataPathTemplate(segments) { KeyAsSegment = keyAsSegment });
            }
        }
    }
}
