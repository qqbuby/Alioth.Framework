/*
 * The MIT License (MIT)
 *
 * Copyright (c) 2016 Roy Xu
 *
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Alioth.Framework {
    /// <summary>
    /// Represents a IoC container.
    /// </summary>
    public sealed class AliothServiceContainer : IAliothServiceContainer {
        private IAliothServiceContainer parent;
        private ConcurrentDictionary<ServiceKey, IObjectBuilder> builderContainer;

        /// <summary>
        /// Gets or sets the description of the IoC container.
        /// </summary>
        public String Description { get; set; }

        /// <summary>
        /// Gets the parent IoC cotnainer.
        /// </summary>
        public IAliothServiceContainer Parent { get { return parent; } }

        /// <summary>
        /// Initializes a new instance of the class <c>Alioth.Framework.AliothServiceContainer</c> with a specified parent IoC container <paramref name="parent"/>.
        /// </summary>
        /// <param name="parent">The parent IoC cotnainer of the new IoC container.</param>
        public AliothServiceContainer(IAliothServiceContainer parent = null) {
            this.builderContainer = new ConcurrentDictionary<ServiceKey, IObjectBuilder>();
            this.parent = parent;
        }

        /// <summary>
        /// Applys a service object type that used to create service objects.
        /// </summary>
        /// <param name="objectType">A <c>System.Type</c> that specifies the service object type to create service object.</param>
        /// <param name="parameters">A <c>IDictionary&lt;String, String^gt;</c> that specifies a dictionary of the dependency constructor of the service object type.</param>
        /// <param name="properties">A <c>IDictionary&lt;String, String^gt;</c> specifies a dictionary to set properties of the service object.</param>
        /// <param name="name">An <c>System.String</c> that specifies the name of service object to get.</param>
        /// <param name="version">An <c>System.String</c> that specifies the version of service object to get.</param>
        /// <returns>An IoC container that implements <c>Alioth.Framework.IAliothServiceContainer</c>.</returns>
        public IAliothServiceContainer Apply(Type objectType, IDictionary<String, String> parameters, IDictionary<String, String> properties, string name, string version) {
            #region precondition
            if (objectType == null) { throw new ArgumentNullException("objectType"); }
#if NET451
                if (!objectType.IsClass) {
#elif DOTNET5_4
            if (!objectType.GetTypeInfo().IsClass) {
#endif
                throw new ArgumentOutOfRangeException("objectType", "The specified object type should be a concrete class.");
            }
            #endregion
#if NET451
            ServiceTypeAtrribute[] attributes = (ServiceTypeAtrribute[])objectType.GetCustomAttributes(typeof(ServiceTypeAtrribute), false);
#elif DOTNET5_4
            ServiceTypeAtrribute[] attributes = objectType.GetTypeInfo().GetCustomAttributes<ServiceTypeAtrribute>(false).ToArray();
#endif
            if (attributes.Length == 0) {
                throw new ArgumentException(String.Format("{0} should be to anotate with {1}", objectType.Name, attributes));
            }
            IObjectBuilder builder = GetBuilder(objectType, parameters, properties, attributes);
            foreach (ServiceTypeAtrribute attribute in attributes) {
                ServiceKey key = ServiceKey.Create(attribute.ServiceType, name, version);
                AddBuilder(key, builder);
            }
            return this;
        }

        /// <summary>
        /// Applys a singleton service object.
        /// </summary>
        /// <typeparam name="T">The service type of the service object.</typeparam>
        /// <typeparam name="O">The type of the service object.</typeparam>
        /// <param name="instance">The service object.</param>
        /// <param name="name">An <c>System.String</c> that specifies the name of service object to get.</param>
        /// <param name="version">An <c>System.String</c> that specifies the version of service object to get.</param>
        /// <returns>An IoC container that implements <c>Alioth.Framework.IAliothServiceContainer</c>.</returns>
        public IAliothServiceContainer Apply<T, O>(O instance, string name, string version) where O : T {
            var key = new ServiceKey(typeof(T), name, version);
            var builder = SingletonBuilder.Create(instance); //TODO create object build with IoC container.
            AddBuilder(key, builder);
            return this;
        }

        /// <summary>
        /// Creates a child IoC container.
        /// </summary>
        /// <param name="description">A <c>System.String</c> that represents the description of the child IoC container.</param>
        /// <returns>An IoC container that implements <c>Alioth.Framework.IAliothServiceContainer</c>.</returns>
        public IAliothServiceContainer CreateChild(string description = null) {
            return new AliothServiceContainer(this) { Description = description };
        }

        /// <summary>
        /// Gets the service object of the specified type.
        /// </summary>
        /// <param name="serviceType">An <c>System.Type</c> that specifies the type of service object to get.</param>
        /// <returns>A service object of type serviceType.-or- null if there is no service object of type serviceType.</returns>
        public object GetService(Type serviceType) {
            Object obj = null;

            var key = ServiceKey.Create(serviceType);
            IObjectBuilder builder;
            if (builderContainer.TryGetValue(key, out builder)) {
                obj = builder.Build();
            } else if (parent != null) {
                obj = parent.GetService(serviceType);
            }
            return obj;
        }

        /// <summary>
        /// Gets the service object of the specified type.
        /// </summary>
        /// <param name="serviceType">An <c>System.Type</c> that specifies the type of service object to get.</param>
        /// <param name="name">An <c>System.String</c> that specifies the name of service object to get.</param>
        /// <param name="version">An <c>System.String</c> that specifies the version of service object to get.</param>
        /// <returns>A service object of type serviceType.-or- null if there is no service object of type serviceType.</returns>
        public object GetService(Type serviceType, string name, string version) {
            Object obj = null;

            var key = ServiceKey.Create(serviceType, name, version);
            IObjectBuilder builder;
            if (builderContainer.TryGetValue(key, out builder)) {
                obj = builder.Build();
            } else if (parent != null) {
                obj = parent.GetService(serviceType);
            }
            return obj;
        }

        private IObjectBuilder GetBuilder(Type objectType, IDictionary<String, String> parameters, IDictionary<String, String> properties, ServiceTypeAtrribute[] attributes) {
            IObjectBuilder builder;

            Boolean isSingleton = attributes.Any(o => o.ReferenceType == ReferenceType.Singleton);
            if (isSingleton) {
                builder = new SingletonBuilder(); //TODO create object build with IoC container.
            } else {
                builder = new ObjectBuilder(); //TODO create object build with IoC container.
            }
            builder.ObjectType = objectType;
            if (parameters != null) {
                foreach (var param in parameters) {
                    builder.Parameters.Add(param.Key, param.Value);
                }
            }
            if (properties != null) {
                foreach (var prop in properties) {
                    builder.Properties.Add(prop.Key, prop.Value);
                }
            }
            builder.Connect(this);
            return builder;
        }

        private void AddBuilder(ServiceKey key, IObjectBuilder builder) {
            if (!builderContainer.TryAdd(key, builder)) {
                throw new ArgumentException(String.Format("An element with the same key:\"{0}\", already exists in the AliothServiceContainer:\"{1}\"", key, Description));
            }
        }
    }
}