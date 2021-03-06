﻿namespace NServiceBus.Features
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Config;
    using Logging;
    using NServiceBus.Sagas;
    using NServiceBus.Sagas.Finders;
    using Saga;

    public class Sagas : IFeature
    {
        public void Initialize()
        {
            Configure.Component<ReplyingToNullOriginatorDispatcher>(DependencyLifecycle.SingleInstance);

            var sagasFound = FindAndConfigureSagasIn(Configure.TypesToScan);

            if (sagasFound)
            {
                InfrastructureServices.Enable<ISagaPersister>();

                Logger.InfoFormat("Sagas found in scanned types, saga persister enabled"); 
            }
            else
            {
               Logger.InfoFormat("The saga feature was enabled but no saga implementations could be found. No need to enable the configured saga persister"); 
            }
                
        }

        /// <summary>
        /// Scans for types relevant to the saga infrastructure.
        /// These include implementers of <see cref="ISaga" /> and <see cref="IFindSagas{T}" />.
        /// </summary>
        /// <param name="types"></param>
        public bool FindAndConfigureSagasIn(IEnumerable<Type> types)
        {
            var sagasWereFound = false;

            foreach (Type t in types)
            {
                if (IsSagaType(t))
                {
                    Configure.Component(t, DependencyLifecycle.InstancePerCall);
                    ConfigureSaga(t);
                    sagasWereFound = true;
                    continue;
                }

                if (IsFinderType(t))
                {
                    Configure.Component(t, DependencyLifecycle.InstancePerCall);
                    ConfigureFinder(t);
                    continue;
                }

                if (IsSagaNotFoundHandler(t))
                {
                    Configure.Component(t, DependencyLifecycle.InstancePerCall);
                    continue;
                }
            }

            CreateAdditionalFindersAsNecessary();

            return sagasWereFound;
        }

        internal static void ConfigureHowToFindSagaWithMessage(Type sagaType, PropertyInfo sagaProp, Type messageType, PropertyInfo messageProp)
        {
            IDictionary<Type, KeyValuePair<PropertyInfo, PropertyInfo>> messageToProperties;
            SagaEntityToMessageToPropertyLookup.TryGetValue(sagaType, out messageToProperties);

            if (messageToProperties == null)
            {
                messageToProperties = new Dictionary<Type, KeyValuePair<PropertyInfo, PropertyInfo>>();
                SagaEntityToMessageToPropertyLookup[sagaType] = messageToProperties;
            }

            messageToProperties[messageType] = new KeyValuePair<PropertyInfo, PropertyInfo>(sagaProp, messageProp);
        }

        private static readonly IDictionary<Type, IDictionary<Type, KeyValuePair<PropertyInfo, PropertyInfo>>> SagaEntityToMessageToPropertyLookup = new Dictionary<Type, IDictionary<Type, KeyValuePair<PropertyInfo, PropertyInfo>>>();

        /// <summary>
        /// Creates an <see cref="NullSagaFinder{T}" /> for each saga type that doesn't have a finder configured.
        /// </summary>
        private void CreateAdditionalFindersAsNecessary()
        {
            foreach (Type sagaEntityType in SagaEntityToMessageToPropertyLookup.Keys)
                foreach (Type messageType in SagaEntityToMessageToPropertyLookup[sagaEntityType].Keys)
                {
                    var pair = SagaEntityToMessageToPropertyLookup[sagaEntityType][messageType];
                    CreatePropertyFinder(sagaEntityType, messageType, pair.Key, pair.Value);
                }

            foreach (Type sagaType in SagaTypeToSagaEntityTypeLookup.Keys)
            {
                Type sagaEntityType = SagaTypeToSagaEntityTypeLookup[sagaType];


                Type nullFinder = typeof(NullSagaFinder<>).MakeGenericType(sagaEntityType);
                Configure.Component(nullFinder, DependencyLifecycle.InstancePerCall);
                ConfigureFinder(nullFinder);

                Type sagaHeaderIdFinder = typeof(HeaderSagaIdFinder<>).MakeGenericType(sagaEntityType);
                Configure.Component(sagaHeaderIdFinder, DependencyLifecycle.InstancePerCall);
                ConfigureFinder(sagaHeaderIdFinder);
            }
        }

        private void CreatePropertyFinder(Type sagaEntityType, Type messageType, PropertyInfo sagaProperty, PropertyInfo messageProperty)
        {
            Type finderType = typeof(PropertySagaFinder<,>).MakeGenericType(sagaEntityType, messageType);

            Configure.Component(finderType, DependencyLifecycle.InstancePerCall)
                .ConfigureProperty("SagaProperty", sagaProperty)
                .ConfigureProperty("MessageProperty", messageProperty);

            ConfigureFinder(finderType);
        }

       
        /// <summary>
        /// Gets the saga type to instantiate and invoke if an existing saga couldn't be found by
        /// the given finder using the given message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="finder"></param>
        /// <returns></returns>
        public static Type GetSagaTypeToStartIfMessageNotFoundByFinder(object message, IFinder finder)
        {
            Type sagaEntityType;
            FinderTypeToSagaEntityTypeLookup.TryGetValue(finder.GetType(), out sagaEntityType);

            if (sagaEntityType == null)
                return null;

            Type sagaType;
            SagaEntityTypeToSagaTypeLookup.TryGetValue(sagaEntityType, out sagaType);

            if (sagaType == null)
                return null;

            List<Type> messageTypes;
            SagaTypeToMessagTypesRequiringSagaStartLookup.TryGetValue(sagaType, out messageTypes);

            if (messageTypes == null)
                return null;

            if (messageTypes.Contains(message.GetType()))
                return sagaType;

            foreach (Type msgTypeHandleBySaga in messageTypes)
                if (msgTypeHandleBySaga.IsAssignableFrom(message.GetType()))
                    return sagaType;

            return null;
        }

        /// <summary>
        /// Finds the types of sagas that can handle the given concrete message type.
        /// </summary>
        /// <param name="messageType">A concrete type for a message object</param>
        /// <returns>The list of saga types.</returns>
        public static List<Type> GetSagaTypesForMessageType(Type messageType)
        {
            var sagas = new List<Type>();

            foreach (Type msgTypeHandled in MessageTypeToSagaTypesLookup.Keys)
                if (msgTypeHandled.IsAssignableFrom(messageType))
                    sagas.AddRange(MessageTypeToSagaTypesLookup[msgTypeHandled]);

            return sagas;
        }

        /// <summary>
        /// Returns the saga type configured for the given entity type.
        /// </summary>
        /// <param name="sagaEntityType"></param>
        /// <returns></returns>
        public static Type GetSagaTypeForSagaEntityType(Type sagaEntityType)
        {
            Type result;
            SagaEntityTypeToSagaTypeLookup.TryGetValue(sagaEntityType, out result);

            return result;
        }

        /// <summary>
        /// Returns the entity type configured for the given saga type.
        /// </summary>
        /// <param name="sagaType"></param>
        /// <returns></returns>
        public static Type GetSagaEntityTypeForSagaType(Type sagaType)
        {
            Type result;
            SagaTypeToSagaEntityTypeLookup.TryGetValue(sagaType, out result);

            return result;
        }

        /// <summary>
        /// Indicates if a saga has been configured to handle the given message type.
        /// </summary>
        /// <param name="messageType"></param>
        /// <returns></returns>
        public static bool IsMessageTypeHandledBySaga(Type messageType)
        {
            if (MessageTypeToSagaTypesLookup.Keys.Contains(messageType))
                return true;

            foreach (Type msgHandledBySaga in MessageTypeToSagaTypesLookup.Keys)
                if (msgHandledBySaga.IsAssignableFrom(messageType))
                    return true;

            return false;
        }

        /// <summary>
        /// Gets a reference to the generic "FindBy" method of the given finder
        /// for the given message type using a hashtable lookup rather than reflection.
        /// </summary>
        /// <param name="finder"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static MethodInfo GetFindByMethodForFinder(IFinder finder, object message)
        {
            MethodInfo result = null;

            IDictionary<Type, MethodInfo> methods;
            FinderTypeToMessageToMethodInfoLookup.TryGetValue(finder.GetType(), out methods);

            if (methods != null)
            {
                methods.TryGetValue(message.GetType(), out result);

                if (result == null)
                    foreach (Type messageType in methods.Keys)
                        if (messageType.IsAssignableFrom(message.GetType()))
                            result = methods[messageType];
            }

            return result;
        }

        /// <summary>
        /// Returns a list of finder object capable of using the given message.
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public static IEnumerable<Type> GetFindersFor(object m)
        {
            foreach (Type finderType in FinderTypeToMessageToMethodInfoLookup.Keys)
            {
                IDictionary<Type, MethodInfo> messageToMethodInfo = FinderTypeToMessageToMethodInfoLookup[finderType];
                if (messageToMethodInfo.ContainsKey(m.GetType()))
                {
                    yield return finderType;
                    continue;
                }

                foreach (Type messageType in messageToMethodInfo.Keys)
                    if (messageType.IsAssignableFrom(m.GetType()))
                        yield return finderType;
            }
        }

        /// <summary>
        /// Returns the list of saga types configured.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<Type> GetSagaDataTypes()
        {
            return SagaTypeToSagaEntityTypeLookup.Values;
        }

        public static bool IsSagaType(Type t)
        {
            return IsCompatible(t, typeof(ISaga));
        }

        static bool IsFinderType(Type t)
        {
            return IsCompatible(t, typeof(IFinder));
        }

        static bool IsSagaNotFoundHandler(Type t)
        {
            return IsCompatible(t, typeof(IHandleSagaNotFound));
        }

        static bool IsCompatible(Type t, Type source)
        {
            return source.IsAssignableFrom(t) && t != source && !t.IsAbstract && !t.IsInterface && !t.IsGenericType;
        }

        static void ConfigureSaga(Type t)
        {
            foreach (Type messageType in GetMessageTypesHandledBySaga(t))
                MapMessageTypeToSagaType(messageType, t);

            foreach (Type messageType in GetMessageTypesThatRequireStartingTheSaga(t))
                MessageTypeRequiresStartingSaga(messageType, t);

            PropertyInfo prop = t.GetProperty("Data");
            MapSagaTypeToSagaEntityType(t, prop.PropertyType);

            if (!typeof(IConfigurable).IsAssignableFrom(t))
                return;

            var defaultConstructor = t.GetConstructor(Type.EmptyTypes);
            if (defaultConstructor == null)
                throw new InvalidOperationException("Sagas which implement IConfigurable, like those which inherit from Saga<T>, must have a default constructor.");

            var saga = Activator.CreateInstance(t) as ISaga;

            var p = t.GetProperty("SagaMessageFindingConfiguration", typeof(IConfigureHowToFindSagaWithMessage));
            if (p != null)
                p.SetValue(saga, SagaMessageFindingConfiguration, null);

            if (saga is IConfigurable)
                (saga as IConfigurable).Configure();
        }


        static void ConfigureFinder(Type t)
        {
            foreach (Type interfaceType in t.GetInterfaces())
            {
                Type[] args = interfaceType.GetGenericArguments();
                if (args.Length != 2)
                    continue;

                Type sagaEntityType = null;
                Type messageType = null;
                foreach (Type typ in args)
                {
                    if (typeof(IContainSagaData).IsAssignableFrom(typ))
                        sagaEntityType = typ;

                    if (MessageConventionExtensions.IsMessageType(typ) || typ == typeof(object))
                        messageType = typ;
                }

                if (sagaEntityType == null || messageType == null)
                    continue;

                Type finderType = typeof(IFindSagas<>.Using<>).MakeGenericType(sagaEntityType, messageType);
                if (!finderType.IsAssignableFrom(t))
                    continue;

                FinderTypeToSagaEntityTypeLookup[t] = sagaEntityType;

                MethodInfo method = t.GetMethod("FindBy", new[] { messageType });

                IDictionary<Type, MethodInfo> methods;
                FinderTypeToMessageToMethodInfoLookup.TryGetValue(t, out methods);

                if (methods == null)
                {
                    methods = new Dictionary<Type, MethodInfo>();
                    FinderTypeToMessageToMethodInfoLookup[t] = methods;
                }

                methods[messageType] = method;
            }
        }

        static IEnumerable<Type> GetMessageTypesHandledBySaga(Type sagaType)
        {
            return GetMessagesCorrespondingToFilterOnSaga(sagaType, typeof(IHandleMessages<>));
        }

        static IEnumerable<Type> GetMessageTypesThatRequireStartingTheSaga(Type sagaType)
        {
            return GetMessagesCorrespondingToFilterOnSaga(sagaType, typeof(IAmStartedByMessages<>));
        }

        static IEnumerable<Type> GetMessagesCorrespondingToFilterOnSaga(Type sagaType, Type filter)
        {
            foreach (Type interfaceType in sagaType.GetInterfaces())
            {
                Type[] types = interfaceType.GetGenericArguments();
                foreach (Type arg in types)
                    if (MessageConventionExtensions.IsMessageType(arg))
                        if (filter.MakeGenericType(arg) == interfaceType)
                            yield return arg;
            }
        }

        static void MapMessageTypeToSagaType(Type messageType, Type sagaType)
        {
            List<Type> sagas;
            MessageTypeToSagaTypesLookup.TryGetValue(messageType, out sagas);

            if (sagas == null)
            {
                sagas = new List<Type>(1);
                MessageTypeToSagaTypesLookup[messageType] = sagas;
            }

            if (!sagas.Contains(sagaType))
                sagas.Add(sagaType);
        }

        static void MapSagaTypeToSagaEntityType(Type sagaType, Type sagaEntityType)
        {
            SagaTypeToSagaEntityTypeLookup[sagaType] = sagaEntityType;
            SagaEntityTypeToSagaTypeLookup[sagaEntityType] = sagaType;
        }

        static void MessageTypeRequiresStartingSaga(Type messageType, Type sagaType)
        {
            List<Type> messages;
            SagaTypeToMessagTypesRequiringSagaStartLookup.TryGetValue(sagaType, out messages);

            if (messages == null)
            {
                messages = new List<Type>(1);
                SagaTypeToMessagTypesRequiringSagaStartLookup[sagaType] = messages;
            }

            if (!messages.Contains(messageType))
                messages.Add(messageType);
        }

        readonly static IDictionary<Type, List<Type>> MessageTypeToSagaTypesLookup = new Dictionary<Type, List<Type>>();

        static readonly IDictionary<Type, Type> SagaEntityTypeToSagaTypeLookup = new Dictionary<Type, Type>();
        static readonly IDictionary<Type, Type> SagaTypeToSagaEntityTypeLookup = new Dictionary<Type, Type>();

        static readonly IDictionary<Type, Type> FinderTypeToSagaEntityTypeLookup = new Dictionary<Type, Type>();
        static readonly IDictionary<Type, IDictionary<Type, MethodInfo>> FinderTypeToMessageToMethodInfoLookup = new Dictionary<Type, IDictionary<Type, MethodInfo>>();

        static readonly IDictionary<Type, List<Type>> SagaTypeToMessagTypesRequiringSagaStartLookup = new Dictionary<Type, List<Type>>();

        static readonly IConfigureHowToFindSagaWithMessage SagaMessageFindingConfiguration = new ConfigureHowToFindSagaWithMessageDispatcher();

        static readonly ILog Logger = LogManager.GetLogger(typeof(ReplyingToNullOriginatorDispatcher));
    }
}