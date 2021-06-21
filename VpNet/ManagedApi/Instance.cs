﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using VpNet.ManagedApi.System;
using VpNet.NativeApi;

namespace VpNet
{
    /// <summary>
    ///     Provides a managed API which offers full encapsulation of the native SDK.
    /// </summary>
    public partial class Instance
    {
        private const string DefaultUniverseHost = "universe.virtualparadise.org";
        private const int DefaultUniversePort = 57000;

        private readonly Dictionary<int, TaskCompletionSource<object>> _objectCompletionSources;
        private readonly Dictionary<int, Avatar> _avatars;
        private readonly Dictionary<string, World> _worlds;
        private TaskCompletionSource<object> _connectCompletionSource;
        private TaskCompletionSource<object> _loginCompletionSource;
        private TaskCompletionSource<object> _enterCompletionSource;
        private IntPtr _instance;
        private NetConfig _netConfig;
        private GCHandle _instanceHandle;

        public Instance()
        {
            Configuration = new InstanceConfiguration();
            _objectCompletionSources = new Dictionary<int, TaskCompletionSource<object>>();
            _worlds = new Dictionary<string, World>();
            _avatars = new Dictionary<int, Avatar>();
            InitOnce();
            InitVpNative();
        }
        
        public delegate void ChatMessageDelegate(Instance sender, ChatMessageEventArgs args);

        public delegate void AvatarChangeDelegate(Instance sender, AvatarChangeEventArgs args);
        public delegate void AvatarEnterDelegate(Instance sender, AvatarEnterEventArgs args);
        public delegate void AvatarLeaveDelegate(Instance sender, AvatarLeaveEventArgs args);
        public delegate void AvatarClickDelegate(Instance sender, AvatarClickEventArgs args);

        public delegate void TeleportDelegate(Instance sender, TeleportEventArgs args);

        public delegate void UserAttributesDelegate(Instance sender, UserAttributesEventArgs args);

        public delegate void WorldListEventDelegate(Instance sender, WorldListEventArgs args);

        public delegate void ObjectCreateDelegate(Instance sender, ObjectCreateArgs args);
        public delegate void ObjectChangeDelegate(Instance sender, ObjectChangeArgs args);
        public delegate void ObjectDeleteDelegate(Instance sender, ObjectDeleteArgs args);
        public delegate void ObjectClickDelegate(Instance sender, ObjectClickArgs args);
        public delegate void ObjectBumpDelegate(Instance sender, ObjectBumpArgs args);

        public delegate void QueryCellResultDelegate(Instance sender, QueryCellResultArgs args);
        public delegate void QueryCellEndDelegate(Instance sender, QueryCellEndArgs args);

        public delegate void WorldSettingsChangedDelegate(Instance sender, WorldSettingsChangedEventArgs args);
        public delegate void WorldDisconnectDelegate(Instance sender, WorldDisconnectEventArgs args);

        public delegate void UniverseDisconnectDelegate(Instance sender, UniverseDisconnectEventArgs args);
        public delegate void JoinDelegate(Instance sender, JoinEventArgs args);

        public delegate void FriendAddCallbackDelegate(Instance sender, FriendAddCallbackEventArgs args);
        public delegate void FriendDeleteCallbackDelegate(Instance sender, FriendDeleteCallbackEventArgs args);
        public delegate void FriendsGetCallbackDelegate(Instance sender, FriendsGetCallbackEventArgs args);

        public delegate void WorldLeaveDelegate(Instance sender, WorldLeaveEventArgs args);
        
        public delegate void WorldEnterDelegate(Instance sender, WorldEnterEventArgs args);

        public event ChatMessageDelegate OnChatMessage;
        public event AvatarEnterDelegate OnAvatarEnter;
        public event AvatarChangeDelegate OnAvatarChange;
        public event AvatarLeaveDelegate OnAvatarLeave;
        public event AvatarClickDelegate OnAvatarClick;
        public event JoinDelegate OnJoin;

        public event TeleportDelegate OnTeleport;
        public event UserAttributesDelegate OnUserAttributes;

        public event ObjectCreateDelegate OnObjectCreate;
        public event ObjectChangeDelegate OnObjectChange;
        public event ObjectDeleteDelegate OnObjectDelete;
        public event ObjectClickDelegate OnObjectClick;
        public event ObjectBumpDelegate OnObjectBump;


        public event WorldListEventDelegate OnWorldList;
        public event WorldSettingsChangedDelegate OnWorldSettingsChanged;
        public event FriendAddCallbackDelegate OnFriendAddCallback;
        public event FriendsGetCallbackDelegate OnFriendsGetCallback;

        public event WorldDisconnectDelegate OnWorldDisconnect;
        public event UniverseDisconnectDelegate OnUniverseDisconnect;

        public event QueryCellResultDelegate OnQueryCellResult;
        public event QueryCellEndDelegate OnQueryCellEnd;

        public event WorldEnterDelegate OnWorldEnter;
        
        public event WorldLeaveDelegate OnWorldLeave;
        
        
        internal event EventDelegate OnChatNativeEvent;
        internal event EventDelegate OnAvatarAddNativeEvent;
        internal event EventDelegate OnAvatarDeleteNativeEvent;
        internal event EventDelegate OnAvatarChangeNativeEvent;
        internal event EventDelegate OnAvatarClickNativeEvent;
        internal event EventDelegate OnWorldListNativeEvent;
        internal event EventDelegate OnObjectChangeNativeEvent;
        internal event EventDelegate OnObjectCreateNativeEvent;
        internal event EventDelegate OnObjectDeleteNativeEvent;
        internal event EventDelegate OnObjectClickNativeEvent;
        internal event EventDelegate OnObjectBumpNativeEvent;
        internal event EventDelegate OnObjectBumpEndNativeEvent;
        internal event EventDelegate OnQueryCellEndNativeEvent;
        internal event EventDelegate OnUniverseDisconnectNativeEvent;
        internal event EventDelegate OnWorldDisconnectNativeEvent;
        internal event EventDelegate OnTeleportNativeEvent;
        internal event EventDelegate OnUserAttributesNativeEvent;
        internal event EventDelegate OnJoinNativeEvent;

        internal event CallbackDelegate OnObjectCreateCallbackNativeEvent;
        internal event CallbackDelegate OnObjectChangeCallbackNativeEvent;
        internal event CallbackDelegate OnObjectDeleteCallbackNativeEvent;
        internal event CallbackDelegate OnObjectGetCallbackNativeEvent;
        internal event CallbackDelegate OnObjectLoadCallbackNativeEvent;
        internal event CallbackDelegate OnFriendAddCallbackNativeEvent;
        internal event CallbackDelegate OnFriendDeleteCallbackNativeEvent;
        internal event CallbackDelegate OnGetFriendsCallbackNativeEvent;

        internal event CallbackDelegate OnJoinCallbackNativeEvent;
        internal event CallbackDelegate OnWorldPermissionUserSetCallbackNativeEvent;
        internal event CallbackDelegate OnWorldPermissionSessionSetCallbackNativeEvent;
        internal event CallbackDelegate OnWorldSettingsSetCallbackNativeEvent;
   
        public InstanceConfiguration Configuration { get; set; }

        /// <summary>
        ///     Gets a read-only view of the avatars currently seen by this instance.
        /// </summary>
        /// <value>A read-only view of the avatars currently seen by this instance.</value>
        public IReadOnlyCollection<Avatar> Avatars => _avatars.Values;

        /// <summary>
        ///     Gets the universe to which this instance is currently connected.
        /// </summary>
        /// <value>The universe to which this instance is currently connected.</value>
        public Universe Universe { get; private set; }

        /// <summary>
        ///     Gets the world to which this instance is currently connected.
        /// </summary>
        /// <value>The world to which this instance is currently connected.</value>
        public World World { get; private set; }

        internal void InitOnce()
        {
            _instanceHandle = GCHandle.Alloc(this);
            _netConfig.Context = GCHandle.ToIntPtr(_instanceHandle);
            _netConfig.Create = Connection.CreateNative;
            _netConfig.Destroy = Connection.DestroyNative;
            _netConfig.Connect = Connection.ConnectNative;
            _netConfig.Receive = Connection.ReceiveNative;
            _netConfig.Send = Connection.SendNative;
            _netConfig.Timeout = Connection.TimeoutNative;

            OnChatNativeEvent += OnChatNative;
            OnAvatarAddNativeEvent += OnAvatarAddNative;
            OnAvatarChangeNativeEvent += OnAvatarChangeNative;
            OnAvatarDeleteNativeEvent += OnAvatarDeleteNative;
            OnAvatarClickNativeEvent += OnAvatarClickNative;
            OnWorldListNativeEvent += OnWorldListNative;
            OnWorldDisconnectNativeEvent += OnWorldDisconnectNative;

            OnObjectChangeNativeEvent += OnObjectChangeNative;
            OnObjectCreateNativeEvent += OnObjectCreateNative;
            OnObjectClickNativeEvent += OnObjectClickNative;
            OnObjectBumpNativeEvent += OnObjectBumpNative;
            OnObjectBumpEndNativeEvent += OnObjectBumpEndNative;
            OnObjectDeleteNativeEvent += OnObjectDeleteNative;

            OnQueryCellEndNativeEvent += OnQueryCellEndNative;
            OnUniverseDisconnectNativeEvent += OnUniverseDisconnectNative;
            OnTeleportNativeEvent += OnTeleportNative;
            OnUserAttributesNativeEvent += OnUserAttributesNative;
            OnJoinNativeEvent += OnJoinNative;

            OnObjectCreateCallbackNativeEvent += OnObjectCreateCallbackNative;
            OnObjectChangeCallbackNativeEvent += OnObjectChangeCallbackNative;
            OnObjectDeleteCallbackNativeEvent += OnObjectDeleteCallbackNative;
            OnObjectGetCallbackNativeEvent += OnObjectGetCallbackNative;
            this.OnObjectLoadCallbackNativeEvent += this.OnObjectLoadCallbackNative;

            OnFriendAddCallbackNativeEvent += OnFriendAddCallbackNative;
            OnFriendDeleteCallbackNativeEvent += OnFriendDeleteCallbackNative;
            OnGetFriendsCallbackNativeEvent += OnGetFriendsCallbackNative;
        }

        private void InitVpNative()
        {
               
            int rc = Functions.vp_init(5);
            if (rc != 0)
            {
                throw new VpException((ReasonCode)rc);
            }

            _instance = Functions.vp_create(ref _netConfig);

            SetNativeEvent(Events.Chat, OnChatNative1);
            SetNativeEvent(Events.AvatarAdd, OnAvatarAddNative1);
            SetNativeEvent(Events.AvatarChange, OnAvatarChangeNative1);
            SetNativeEvent(Events.AvatarDelete, OnAvatarDeleteNative1);
            SetNativeEvent(Events.AvatarClick, OnAvatarClickNative1);
            SetNativeEvent(Events.WorldList, OnWorldListNative1);
            SetNativeEvent(Events.WorldSetting, OnWorldSettingNative1);
            SetNativeEvent(Events.WorldSettingsChanged, OnWorldSettingsChangedNative1);
            SetNativeEvent(Events.ObjectChange, OnObjectChangeNative1);
            SetNativeEvent(Events.Object, OnObjectCreateNative1);
            SetNativeEvent(Events.ObjectClick, OnObjectClickNative1);
            SetNativeEvent(Events.ObjectBumpBegin, OnObjectBumpNative1);
            SetNativeEvent(Events.ObjectBumpEnd, OnObjectBumpEndNative1);
            SetNativeEvent(Events.ObjectDelete, OnObjectDeleteNative1);
            SetNativeEvent(Events.QueryCellEnd, OnQueryCellEndNative1);
            SetNativeEvent(Events.UniverseDisconnect, OnUniverseDisconnectNative1);
            SetNativeEvent(Events.WorldDisconnect, OnWorldDisconnectNative1);
            SetNativeEvent(Events.Teleport, OnTeleportNative1);
            SetNativeEvent(Events.UserAttributes, OnUserAttributesNative1);
            SetNativeEvent(Events.Join, OnJoinNative1);
            SetNativeCallback(Callbacks.ObjectAdd, OnObjectCreateCallbackNative1);
            SetNativeCallback(Callbacks.ObjectChange, OnObjectChangeCallbackNative1);
            SetNativeCallback(Callbacks.ObjectDelete, OnObjectDeleteCallbackNative1);
            SetNativeCallback(Callbacks.ObjectGet, OnObjectGetCallbackNative1);
            SetNativeCallback(Callbacks.ObjectLoad, this.OnObjectLoadCallbackNative1);
            SetNativeCallback(Callbacks.FriendAdd, OnFriendAddCallbackNative1);
            SetNativeCallback(Callbacks.FriendDelete, OnFriendDeleteCallbackNative1);
            SetNativeCallback(Callbacks.GetFriends, OnGetFriendsCallbackNative1);
            SetNativeCallback(Callbacks.Login, OnLoginCallbackNative1);
            SetNativeCallback(Callbacks.Enter, OnEnterCallbackNativeEvent1);
            //SetNativeCallback(Callbacks.Join, OnJoinCallbackNativeEvent1);
            SetNativeCallback(Callbacks.ConnectUniverse, OnConnectUniverseCallbackNative1);
            //SetNativeCallback(Callbacks.WorldPermissionUserSet, OnWorldPermissionUserSetCallbackNative1);
            //SetNativeCallback(Callbacks.WorldPermissionSessionSet, OnWorldPermissionSessionSetCallbackNative1);
            //SetNativeCallback(Callbacks.WorldSettingSet, OnWorldSettingsSetCallbackNative1);
        }

        internal void OnObjectCreateCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnObjectCreateCallbackNativeEvent(instance, rc, reference); } }
        internal void OnObjectChangeCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnObjectChangeCallbackNativeEvent(instance, rc, reference); } }
        internal void OnObjectDeleteCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnObjectDeleteCallbackNativeEvent(instance, rc, reference); } }
        internal void OnObjectGetCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnObjectGetCallbackNativeEvent(instance, rc, reference); } }
        internal void OnFriendAddCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnFriendAddCallbackNativeEvent(instance, rc, reference); } }
        internal void OnFriendDeleteCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnFriendDeleteCallbackNativeEvent(instance, rc, reference); } }
        internal void OnGetFriendsCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnFriendDeleteCallbackNativeEvent(instance, rc, reference); } }
        internal void OnObjectLoadCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnObjectLoadCallbackNativeEvent(instance, rc, reference); } }
        internal void OnLoginCallbackNative1(IntPtr instance, int rc, int reference)
        {
            lock (this)
            {
                SetCompletionResult(_loginCompletionSource, rc, null);
            }
        }
        internal void OnEnterCallbackNativeEvent1(IntPtr instance, int rc, int reference)
        {
            lock (this)
            {
                SetCompletionResult(_enterCompletionSource, rc, null);
                OnWorldEnter?.Invoke(this, new WorldEnterEventArgs(World));
            }
        }
        internal void OnJoinCallbackNativeEvent1(IntPtr instance, int rc, int reference) { lock (this) { OnJoinCallbackNativeEvent(instance, rc, reference); } }
        internal void OnConnectUniverseCallbackNative1(IntPtr instance, int rc, int reference)
        {
            lock (this)
            {
                SetCompletionResult(_connectCompletionSource, rc, null);
            }
        }
        internal void OnWorldPermissionUserSetCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnWorldPermissionUserSetCallbackNativeEvent(instance, rc, reference); } }
        internal void OnWorldPermissionSessionSetCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnWorldPermissionSessionSetCallbackNativeEvent(instance, rc, reference); } }
        internal void OnWorldSettingsSetCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnWorldSettingsSetCallbackNativeEvent(instance, rc, reference); } }



        internal void OnChatNative1(IntPtr instance) { lock (this) { OnChatNativeEvent(instance); } }
        internal void OnAvatarAddNative1(IntPtr instance) { lock (this) { OnAvatarAddNativeEvent(instance); } }
        internal void OnAvatarChangeNative1(IntPtr instance) { lock (this) { OnAvatarChangeNativeEvent(instance); } }
        internal void OnAvatarDeleteNative1(IntPtr instance) { lock (this) { OnAvatarDeleteNativeEvent(instance); } }
        internal void OnAvatarClickNative1(IntPtr instance) { lock (this) { OnAvatarClickNativeEvent(instance); } }
        internal void OnWorldListNative1(IntPtr instance) { lock (this) { OnWorldListNativeEvent(instance); } }
        internal void OnWorldDisconnectNative1(IntPtr instance) { lock (this) { OnWorldDisconnectNativeEvent(instance); } }
        internal void OnWorldSettingsChangedNative1(IntPtr instance) { lock (this) { OnWorldSettingsChangedNativeEvent(instance); } }
        internal void OnWorldSettingNative1(IntPtr instance) { lock (this) { OnWorldSettingNativeEvent(instance); } }
        internal void OnObjectChangeNative1(IntPtr instance) { lock (this) { OnObjectChangeNativeEvent(instance); } }
        internal void OnObjectCreateNative1(IntPtr instance) { lock (this) { OnObjectCreateNativeEvent(instance); } }
        internal void OnObjectClickNative1(IntPtr instance) { lock (this) { OnObjectClickNativeEvent(instance); } }
        internal void OnObjectBumpNative1(IntPtr instance) { lock (this) { OnObjectBumpNativeEvent(instance); } }
        internal void OnObjectBumpEndNative1(IntPtr instance) { lock (this) { OnObjectBumpEndNativeEvent(instance); } }
        internal void OnObjectDeleteNative1(IntPtr instance) { lock (this) { OnObjectDeleteNativeEvent(instance); } }
        internal void OnQueryCellEndNative1(IntPtr instance) { lock (this) { OnQueryCellEndNativeEvent(instance); } }
        internal void OnUniverseDisconnectNative1(IntPtr instance) { lock (this) { OnUniverseDisconnectNativeEvent(instance); } }
        internal void OnTeleportNative1(IntPtr instance) { lock (this) { OnTeleportNativeEvent(instance); } }
        internal void OnUserAttributesNative1(IntPtr instance) { lock (this){OnUserAttributesNativeEvent(instance);} }
        internal void OnJoinNative1(IntPtr instance) { lock (this) { OnJoinNativeEvent(instance); } }

        #region Methods

        private void SetCompletionResult(int referenceNumber, int rc, object result)
        {
            var tcs = _objectCompletionSources[referenceNumber];
            SetCompletionResult(tcs, rc, result);
        }

        private static void SetCompletionResult(TaskCompletionSource<object> tcs, int rc, object result)
        {
            if (rc != 0)
            {
                tcs.SetException(new VpException((ReasonCode)rc));
            }
            else
            {
                tcs.SetResult(result);
            }
        }

        private static void CheckReasonCode(int rc)
        {
            if (rc != 0)
            {
                throw new VpException((ReasonCode)rc);
            }
        }

        #region IUniverseFunctions Implementations

        /// <summary>
        ///     Establishes a connection to default Virtual Paradise universe.
        /// </summary>
        public Task ConnectAsync()
        {
            return ConnectAsync(DefaultUniverseHost, DefaultUniversePort);
        }

        /// <summary>
        ///     Establishes a connection to the universe at the specified remote endpoint.
        /// </summary>
        /// <param name="host">The remote host.</param>
        /// <param name="port">The remote port.</param>
        public Task ConnectAsync(string host, int port)
        {
            EndPoint remoteEP = IPAddress.TryParse(host, out var ipAddress)
                ? (EndPoint) new IPEndPoint(ipAddress, port)
                : new DnsEndPoint(host, port);
            
            return ConnectAsync(remoteEP);
        }

        /// <summary>
        ///     Establishes a connection to the universe at the specified remote endpoint.
        /// </summary>
        /// <param name="remoteEP">The remote endpoint of the universe.</param>
        public Task ConnectAsync(EndPoint remoteEP)
        {
            string host;
            int port;

            switch (remoteEP)
            {
                case null:
                    host = DefaultUniverseHost;
                    port = DefaultUniversePort;
                    remoteEP = new DnsEndPoint(host, port); // reconstruct endpoint for Universe ctor
                    break;
                    
                case IPEndPoint ip:
                    host = ip.Address.ToString();
                    port = ip.Port;
                    break;
                
                case DnsEndPoint dns:
                    host = dns.Host;
                    port = dns.Port;
                    break;
                
                default:
                    throw new ArgumentException("The specified remote endpoint is not supported.", nameof(remoteEP));
            }
            
            Universe = new Universe(remoteEP);

            lock (this)
            {
                _connectCompletionSource = new TaskCompletionSource<object>();
                int reason = Functions.vp_connect_universe(_instance, host, port);
                if (reason != 0)
                {
                    return Task.FromException<VpException>(new VpException((ReasonCode) reason));
                }

                return _connectCompletionSource.Task;
            }
        }

        public virtual async Task LoginAndEnterAsync(bool announceAvatar = true)
        {
            await ConnectAsync();
            await LoginAsync();
            await EnterAsync();
            if (announceAvatar)
            {
                UpdateAvatar();
            }
        }

        public virtual async Task LoginAsync()
        {
            if (Configuration == null ||
                string.IsNullOrEmpty(Configuration.BotName) ||
                string.IsNullOrEmpty(Configuration.Password) ||
                string.IsNullOrEmpty(Configuration.UserName)
                )
            {
                throw new ArgumentException("Can't login because of Incomplete login configuration.");
            }

            await LoginAsync(Configuration.UserName, Configuration.Password, Configuration.BotName);
        }

        public virtual Task LoginAsync(string username, string password, string botname)
        {
            lock (this)
            {
                Configuration.BotName = botname;
                Configuration.UserName = username;
                Configuration.Password = password;
                Functions.vp_string_set(_instance, StringAttribute.ApplicationName, Configuration.ApplicationName);
                Functions.vp_string_set(_instance, StringAttribute.ApplicationVersion, Configuration.ApplicationVersion);

                _loginCompletionSource = new TaskCompletionSource<object>();
                var rc = Functions.vp_login(_instance, username, password, botname);
                if (rc != 0)
                {
                    return Task.FromException(new VpException((ReasonCode)rc));
                }

                return _loginCompletionSource.Task;
            }
        }

        #endregion

        #region WorldFunctions Implementations
        [Obsolete("No longer necessary for network IO to occur")]
        public virtual void Wait(int milliseconds = 10)
        {
            Thread.Sleep(milliseconds);
        }

        public virtual Task EnterAsync(string worldname)
        {
            return EnterAsync(new World { Name = worldname });
        }

        public virtual Task EnterAsync()
        {
            if (Configuration == null || Configuration.World == null || string.IsNullOrEmpty(Configuration.World.Name))
                throw new ArgumentException("Can't login because of Incomplete instance world configuration.");
            return EnterAsync(Configuration.World);
        }

        public virtual Task EnterAsync(World world)
        {
            lock (this)
            {
                Configuration.World = world;

                _enterCompletionSource = new TaskCompletionSource<object>();
                var rc = Functions.vp_enter(_instance, world.Name);
                if (rc != 0)
                {
                    return Task.FromException(new VpException((ReasonCode)rc));
                }

                return _enterCompletionSource.Task;
            }
        }

        /// <summary>
        ///     If connected to a world, returns the state of this instance's avatar.
        /// </summary>
        /// <returns>An instance of <see cref="Avatar" />, encapsulating the state of this instance.</returns>
        public Avatar My()
        {
            Avatar avatar;

            if (World is null) return null;
            
            lock (this)
            {
                int userId = Functions.vp_int(_instance, IntegerAttribute.MyUserId);
                int type = Functions.vp_int(_instance, IntegerAttribute.MyType);
                string name = Configuration.BotName;

                double x = Functions.vp_double(_instance, FloatAttribute.MyX);
                double y = Functions.vp_double(_instance, FloatAttribute.MyY);
                double z = Functions.vp_double(_instance, FloatAttribute.MyZ);
                
                double pitch = Functions.vp_double(_instance, FloatAttribute.MyPitch);
                double yaw = Functions.vp_double(_instance, FloatAttribute.MyYaw);

                var position = new Vector3(x, y, z);
                var rotation = new Vector3(pitch, yaw, 0);

                avatar = new Avatar(userId, 0, name, type, position, rotation, DateTimeOffset.Now,
                    Configuration.ApplicationName, Configuration.ApplicationVersion);
            }

            return avatar;
        }

        /// <summary>
        /// Leave the current world
        /// </summary>
        public virtual void Leave()
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_leave(_instance));
                OnWorldLeave?.Invoke(this, new WorldLeaveEventArgs(Configuration.World));
            }
        }

        public virtual void Disconnect()
        {
            _avatars.Clear();
            Functions.vp_destroy(_instance);
            InitVpNative();
            OnUniverseDisconnect?.Invoke(this, new UniverseDisconnectEventArgs(Universe, DisconnectType.UserDisconnected));

            Universe = null;
        }

        public virtual void ListWorlds()
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_list(_instance, 0));
            }
        }

        #endregion

        #region IQueryCellFunctions Implementation

        public virtual void QueryCell(int cellX, int cellZ)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_query_cell(_instance, cellX, cellZ));
            }
        }

        public virtual void QueryCell(int cellX, int cellZ, int revision)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_query_cell_revision(_instance, cellX, cellZ, revision));
            }
        }

        #endregion

        #region VpObjectFunctions implementations

        public void ClickObject(VpObject vpObject)
        {
            lock (this)
            {
                ClickObject(vpObject.Id);
            }
        }

        public void ClickObject(int objectId)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_object_click(_instance, objectId, 0, 0, 0, 0));
            }
        }

        public void ClickObject(VpObject vpObject, Avatar avatar)
        {
            lock (this)
            {
                ClickObject(vpObject.Id, avatar.Session);
            }
        }

        public void ClickObject(VpObject vpObject, Avatar avatar, Vector3 worldHit)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_object_click(_instance, vpObject.Id, avatar.Session, (float)worldHit.X, (float)worldHit.Y, (float)worldHit.Z));
            }
        }

        public void ClickObject(VpObject vpObject, Vector3 worldHit)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_object_click(_instance, vpObject.Id, 0, (float)worldHit.X, (float)worldHit.Y, (float)worldHit.Z));
            }
        }

        public void ClickObject(int objectId,int toSession, double worldHitX, double worldHitY, double worldHitZ)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_object_click(_instance, objectId, toSession, (float)worldHitX, (float)worldHitY, (float)worldHitZ));
            }
        }

        public void ClickObject(int objectId, double worldHitX, double worldHitY, double worldHitZ)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_object_click(_instance, objectId, 0, (float)worldHitX, (float)worldHitY, (float)worldHitZ));
            }
        }

        public void ClickObject(int objectId, int toSession)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_object_click(_instance, objectId, toSession, 0, 0, 0));
            }
        }

        public virtual Task DeleteObjectAsync(VpObject vpObject)
        {
            var referenceNumber = ObjectReferenceCounter.GetNextReference();
            var tcs = new TaskCompletionSource<object>();
            lock (this)
            {
                _objectCompletionSources.Add(referenceNumber, tcs);
                Functions.vp_int_set(_instance, IntegerAttribute.ReferenceNumber, referenceNumber);

                int rc = Functions.vp_object_delete(_instance,vpObject.Id);
                if (rc != 0)
                {
                    _objectCompletionSources.Remove(referenceNumber);
                    throw new VpException((ReasonCode)rc);
                }
            }

            return tcs.Task;
        }

        public virtual async Task<int> LoadObjectAsync(VpObject vpObject)
        {
            var referenceNumber = ObjectReferenceCounter.GetNextReference();
            var tcs = new TaskCompletionSource<object>();
            lock (this)
            {
                _objectCompletionSources.Add(referenceNumber, tcs);
                Functions.vp_int_set(_instance, IntegerAttribute.ReferenceNumber, referenceNumber);
                Functions.vp_int_set(_instance, IntegerAttribute.ObjectId, vpObject.Id);
                Functions.vp_string_set(_instance, StringAttribute.ObjectAction, vpObject.Action);
                Functions.vp_string_set(_instance, StringAttribute.ObjectDescription, vpObject.Description);
                Functions.vp_string_set(_instance, StringAttribute.ObjectModel, vpObject.Model);
                Functions.SetData(_instance, DataAttribute.ObjectData, vpObject.Data);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationX, vpObject.Rotation.X);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationY, vpObject.Rotation.Y);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationZ, vpObject.Rotation.Z);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectX, vpObject.Position.X);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectY, vpObject.Position.Y);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectZ, vpObject.Position.Z);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationAngle, vpObject.Angle);
                Functions.vp_int_set(_instance, IntegerAttribute.ObjectType, vpObject.ObjectType);
                Functions.vp_int_set(_instance, IntegerAttribute.ObjectUserId, vpObject.Owner);
                Functions.vp_int_set(_instance, IntegerAttribute.ObjectTime, (int)vpObject.Time.ToUnixTimeSeconds());

                int rc = Functions.vp_object_load(_instance);
                if (rc != 0)
                {
                    _objectCompletionSources.Remove(referenceNumber);
                    throw new VpException((ReasonCode)rc);
                }
            }

            var id = (int)await tcs.Task.ConfigureAwait(false);
            vpObject.Id = id;

            return id;
        }

        public virtual async Task<int> AddObjectAsync(VpObject vpObject)
        {
            var referenceNumber = ObjectReferenceCounter.GetNextReference();
            var tcs = new TaskCompletionSource<object>();
            lock (this)
            {
                _objectCompletionSources.Add(referenceNumber, tcs);
                Functions.vp_int_set(_instance, IntegerAttribute.ReferenceNumber, referenceNumber);
                Functions.vp_int_set(_instance, IntegerAttribute.ObjectId, vpObject.Id);
                Functions.vp_string_set(_instance, StringAttribute.ObjectAction, vpObject.Action);
                Functions.vp_string_set(_instance, StringAttribute.ObjectDescription, vpObject.Description);
                Functions.vp_string_set(_instance, StringAttribute.ObjectModel, vpObject.Model);
                Functions.SetData(_instance, DataAttribute.ObjectData, vpObject.Data);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationX, vpObject.Rotation.X);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationY, vpObject.Rotation.Y);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationZ, vpObject.Rotation.Z);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectX, vpObject.Position.X);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectY, vpObject.Position.Y);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectZ, vpObject.Position.Z);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationAngle, vpObject.Angle);
                Functions.vp_int_set(_instance, IntegerAttribute.ObjectType, vpObject.ObjectType);

                int rc = Functions.vp_object_add(_instance);
                if (rc != 0)
                {
                    _objectCompletionSources.Remove(referenceNumber);
                    throw new VpException((ReasonCode)rc);
                }
            }

            var id = (int)await tcs.Task.ConfigureAwait(false);
            vpObject.Id = id;

            return id;
        }

        public virtual Task ChangeObjectAsync(VpObject vpObject)
        {
            var referenceNumber = ObjectReferenceCounter.GetNextReference();
            var tcs = new TaskCompletionSource<object>();
            lock (this)
            {
                _objectCompletionSources.Add(referenceNumber, tcs);
                Functions.vp_int_set(_instance, IntegerAttribute.ReferenceNumber, referenceNumber);
                Functions.vp_int_set(_instance, IntegerAttribute.ObjectId, vpObject.Id);
                Functions.vp_string_set(_instance, StringAttribute.ObjectAction, vpObject.Action);
                Functions.vp_string_set(_instance, StringAttribute.ObjectDescription, vpObject.Description);
                Functions.vp_string_set(_instance, StringAttribute.ObjectModel, vpObject.Model);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationX, vpObject.Rotation.X);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationY, vpObject.Rotation.Y);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationZ, vpObject.Rotation.Z);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectX, vpObject.Position.X);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectY, vpObject.Position.Y);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectZ, vpObject.Position.Z);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationAngle, vpObject.Angle);
                Functions.vp_int_set(_instance, IntegerAttribute.ObjectType, vpObject.ObjectType);

                int rc = Functions.vp_object_change(_instance);
                if (rc != 0)
                {
                    _objectCompletionSources.Remove(referenceNumber);
                    throw new VpException((ReasonCode)rc);
                }
            }


            return tcs.Task;
        }

        public virtual async Task<VpObject> GetObjectAsync(int id)
        {
            var referenceNumber = ObjectReferenceCounter.GetNextReference();
            var tcs = new TaskCompletionSource<object>();
            lock (this)
            {
                _objectCompletionSources.Add(referenceNumber, tcs);
                Functions.vp_int_set(_instance, IntegerAttribute.ReferenceNumber, referenceNumber);
                var rc = Functions.vp_object_get(_instance, id);
                if (rc != 0)
                {
                    _objectCompletionSources.Remove(referenceNumber);
                    throw new VpException((ReasonCode)rc);
                }
            }

            var obj = (VpObject)await tcs.Task.ConfigureAwait(false);
            return obj;
        }

        #endregion

        #region ITeleportFunctions Implementations

        public virtual void TeleportAvatar(int targetSession, string world, double x, double y, double z, double yaw, double pitch)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_teleport_avatar(_instance, targetSession, world, (float)x, (float)y,
                                                             (float)z, (float)yaw, (float)pitch));
            }
        }

        public virtual void TeleportAvatar(Avatar avatar, string world, double x, double y, double z, double yaw, double pitch)
        {
            TeleportAvatar(avatar.Session, world, (float)x, (float)y, (float)z, (float)yaw, (float)pitch);
        }

        public virtual void TeleportAvatar(Avatar avatar, string world, Vector3 position, double yaw, double pitch)
        {
            TeleportAvatar(avatar.Session, world, (float)position.X, (float)position.Y, (float)position.Z, (float)yaw, (float)pitch);
        }

        public virtual void TeleportAvatar(int targetSession, string world, Vector3 position, double yaw, double pitch)
        {
            TeleportAvatar(targetSession, world, (float)position.X, (float)position.Y, (float)position.Z, (float)yaw, (float)pitch);

        }

        public virtual void TeleportAvatar(Avatar avatar, string world, Vector3 position, Vector3 rotation)
        {
            TeleportAvatar(avatar.Session, world, (float)position.X, (float)position.Y, (float)position.Z,
                           (float)rotation.Y, (float)rotation.X);
        }

        public void TeleportAvatar(Avatar avatar, World world, Vector3 position, Vector3 rotation)
        {
            TeleportAvatar(avatar.Session, world.Name, (float)position.X, (float)position.Y, (float)position.Z,
                           (float)rotation.Y, (float)rotation.X);
        }

        public virtual void TeleportAvatar(Avatar avatar, Vector3 position, Vector3 rotation)
        {
            TeleportAvatar(avatar.Session, string.Empty, (float)position.X, (float)position.Y, (float)position.Z,
                           (float)rotation.Y, (float)rotation.X);
        }

        public virtual void TeleportAvatar(Avatar avatar)
        {
            TeleportAvatar(avatar.Session, string.Empty, (float)avatar.Position.X, (float)avatar.Position.Y,
                           (float)avatar.Position.Z, (float)avatar.Rotation.Y, (float)avatar.Rotation.X);
        }

        #endregion

        #region AvatarFunctions Implementations.

        public virtual void GetUserProfile(int userId)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_user_attributes_by_id(_instance, userId));
            }
        }

        [Obsolete]
        public virtual void GetUserProfile(string userName)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_user_attributes_by_name(_instance, userName));
            }
        }

        public virtual void GetUserProfile(Avatar profile)
        {
            GetUserProfile(profile.UserId);
        }

        public virtual void UpdateAvatar(double x = 0.0f, double y = 0.0f, double z = 0.0f,double yaw = 0.0f, double pitch = 0.0f)
        {
            lock (this)
            {
                Functions.vp_double_set(_instance, FloatAttribute.MyX, x);
                Functions.vp_double_set(_instance, FloatAttribute.MyY, y);
                Functions.vp_double_set(_instance, FloatAttribute.MyZ, z);
                Functions.vp_double_set(_instance, FloatAttribute.MyYaw, yaw);
                Functions.vp_double_set(_instance, FloatAttribute.MyPitch, pitch);
                CheckReasonCode(Functions.vp_state_change(_instance));

            }
        }

        public void UpdateAvatar(Vector3 position)
        {
            UpdateAvatar(position.X, position.Y, position.Z);
        }

        public void UpdateAvatar(Vector3 position, Vector3 rotation)
        {
            UpdateAvatar(position.X, position.Y, position.Z, rotation.X, rotation.Y);
        }

        public void AvatarClick(int session)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_avatar_click(_instance, session));
            }
        }

        public void AvatarClick(Avatar avatar)
        {
            AvatarClick(avatar.Session);
        }

        #endregion

        #region IChatFunctions Implementations

        public virtual void Say(string message)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_say(_instance, message));
            }
        }

        public virtual void Say(string format, params object[] arg)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_say(_instance, string.Format(format, arg)));
            }
        }

        public void ConsoleMessage(int targetSession, string name, string message, TextEffectTypes effects = 0, byte red = 0, byte green = 0, byte blue = 0)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_console_message(_instance, targetSession, name, message, (int)effects, red, green, blue));
            }
        }

        public void ConsoleMessage(Avatar avatar, string name, string message, Color color, TextEffectTypes effects = 0)
        {
            ConsoleMessage(avatar.Session, name, message, effects, color.R, color.G, color.B);
        }

        public void ConsoleMessage(int targetSession, string name, string message, Color color, TextEffectTypes effects = 0)
        {
            ConsoleMessage(targetSession, name, message, effects, color.R, color.G, color.B);
        }

        public void ConsoleMessage(string name, string message, Color color, TextEffectTypes effects = 0)
        {
            ConsoleMessage(0, name, message, effects, color.R, color.G, color.B);
        }

        public void ConsoleMessage(string message, Color color, TextEffectTypes effects = 0)
        {
            ConsoleMessage(0, string.Empty, message, effects, color.R, color.G, color.B);
        }

        public void ConsoleMessage(string message)
        {
            ConsoleMessage(0, string.Empty, message, 0, 0, 0, 0);
        }

        public virtual void ConsoleMessage(Avatar avatar, string name, string message, TextEffectTypes effects = 0, byte red = 0, byte green = 0, byte blue = 0)
        {
            ConsoleMessage(avatar.Session, name, message, effects, red, green, blue);
        }

        public virtual void UrlSendOverlay(Avatar avatar, string url)
        {
            UrlSendOverlay(avatar.Session, url);
        }

        public virtual void UrlSendOverlay(Avatar avatar, Uri url)
        {
            UrlSendOverlay(avatar.Session, url.AbsoluteUri);
        }

        public virtual void UrlSendOverlay(int avatarSession, string url)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_url_send(_instance, avatarSession, url, (int)UrlTarget.UrlTargetOverlay));
            }
        }

        public virtual void UrlSendOverlay(int avatarSession, Uri url)
        {
            UrlSendOverlay(avatarSession, url.AbsoluteUri);
        }

        public virtual void UrlSend(Avatar avatar, string url)
        {
            UrlSend(avatar.Session, url);
        }

        public virtual void UrlSend(Avatar avatar, Uri url)
        {
            UrlSend(avatar.Session, url.AbsoluteUri);
        }

        public virtual void UrlSend(int avatarSession, string url)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_url_send(_instance, avatarSession, url, (int)UrlTarget.UrlTargetBrowser));
            }
        }

        public virtual void UrlSend(int avatarSession, Uri url)
        {
            UrlSend(avatarSession, url.AbsoluteUri);
        }

        #endregion

        #region IJoinFunctions Implementations
        public virtual void Join(Avatar avatar)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_join(_instance, avatar.UserId));
            }
        }

        public virtual void Join(int userId)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_join(_instance, userId));
            }
        }


        public virtual void JoinAccept(int requestId, string world, Vector3 location, float yaw, float pitch)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_join_accept(_instance, requestId, world,location.X,location.Y,location.Z,yaw,pitch));
            }
        }

        public virtual void JoinAccept(int requestId, string world, double x, double y, double z, float yaw, float pitch)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_join_accept(_instance, requestId, world, x, y, z, yaw, pitch));
            }
        }

        public virtual void JoinDecline(int requestId)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_join_decline(_instance, requestId));
            }
        }

        #endregion

        #region  WorldPermissionFunctions Implementations

        public virtual void WorldPermissionUser(string permission, int userId, int enable)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, permission, userId, enable));
            }
        }

        public virtual void WorldPermissionUserEnable(WorldPermissions permission, Avatar avatar)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, Enum.GetName(typeof(WorldPermissions), permission),avatar.UserId,1));
            }
        }

        public virtual void WorldPermissionUserEnable(WorldPermissions permission, int userId)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, Enum.GetName(typeof(WorldPermissions), permission), userId, 1));
            }
        }

        public virtual void WorldPermissionUserDisable(WorldPermissions permission, Avatar avatar)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, Enum.GetName(typeof(WorldPermissions), permission), avatar.UserId, 0));
            }
        }

        public virtual void WorldPermissionUserDisable(WorldPermissions permission, int userId)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, Enum.GetName(typeof(WorldPermissions), permission), userId, 0));
            }
        }

        public virtual void WorldPermissionSession(string permission, int sessionId, int enable)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_session_set(_instance, permission, sessionId, enable));
            }
        }

        public virtual void WorldPermissionSessionEnable(WorldPermissions permission, Avatar avatar)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, Enum.GetName(typeof(WorldPermissions), permission), avatar.Session, 1));
            }
        }

        public virtual void WorldPermissionSessionEnable(WorldPermissions permission, int session)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, Enum.GetName(typeof(WorldPermissions), permission), session, 1));
            }
        }


        public virtual void WorldPermissionSessionDisable(WorldPermissions permission, Avatar avatar)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, Enum.GetName(typeof(WorldPermissions), permission), avatar.Session, 0));
            }
        }

        public virtual void WorldPermissionSessionDisable(WorldPermissions permission, int session)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, Enum.GetName(typeof(WorldPermissions), permission), session, 0));
            }
        }

        #endregion

        #region WorldSettingsFunctions Implementations

        public virtual void WorldSettingSession(string setting, string value, Avatar toAvatar)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_setting_set(_instance, setting, value, toAvatar.Session));
            }
        }

        public virtual void WorldSettingSession(string setting, string value, int  toSession)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_setting_set(_instance, setting, value, toSession));
            }
        }

        #endregion

        #endregion

        #region Events

        private readonly Dictionary<Events, EventDelegate> _nativeEvents = new Dictionary<Events, EventDelegate>();
        private readonly Dictionary<Callbacks, CallbackDelegate> _nativeCallbacks = new Dictionary<Callbacks, CallbackDelegate>();





        private void SetNativeEvent(Events eventType, EventDelegate eventFunction)
        {
            _nativeEvents[eventType] = eventFunction;
            Functions.vp_event_set(_instance, (int)eventType, eventFunction);
        }

        private void SetNativeCallback(Callbacks callbackType, CallbackDelegate callbackFunction)
        {
            _nativeCallbacks[callbackType] = callbackFunction;
            Functions.vp_callback_set(_instance, (int)callbackType, callbackFunction);
        }

        #endregion

        #region CallbackHandlers

        private void OnObjectCreateCallbackNative(IntPtr sender, int rc, int reference)
        {
            lock (this)
            {
                SetCompletionResult(reference, rc, Functions.vp_int(sender, IntegerAttribute.ObjectId));
            }
        }

        private void OnObjectChangeCallbackNative(IntPtr sender, int rc, int reference)
        {
            lock (this)
            {
                SetCompletionResult(reference, rc, null);
            }
        }

        private void OnObjectDeleteCallbackNative(IntPtr sender, int rc, int reference)
        {
            lock (this)
            {
                SetCompletionResult(reference, rc, null);
            }
        }

        void OnObjectGetCallbackNative(IntPtr sender, int rc, int reference)
        {
            lock (this)
            {
                GetVpObject(sender, out VpObject vpObject);

                SetCompletionResult(reference, rc, vpObject);
            }
        }

        private void OnObjectLoadCallbackNative(IntPtr sender, int rc, int reference)
        {
            lock (this)
            {
                SetCompletionResult(reference, rc, Functions.vp_int(sender, IntegerAttribute.ObjectId));
            }
        }
        #endregion

        #region Event handlers

        private void OnUserAttributesNative(IntPtr sender)
        {
            if (OnUserAttributes == null)
                return;
            
            UserAttributes attributes;
            
            lock (this)
            {
                int id = Functions.vp_int(sender, IntegerAttribute.UserId);
                string name = Functions.vp_string(sender, StringAttribute.UserName);
                string email = Functions.vp_string(sender, StringAttribute.UserEmail);
                int lastLoginTimestamp = Functions.vp_int(sender, IntegerAttribute.UserLastLogin);
                int registrationDateTimestamp = Functions.vp_int(sender, IntegerAttribute.UserRegistrationTime);
                int onlineTimeSeconds = Functions.vp_int(sender, IntegerAttribute.UserOnlineTime);

                DateTimeOffset lastLogin = DateTimeOffset.FromUnixTimeSeconds(lastLoginTimestamp);
                DateTimeOffset registrationDate = DateTimeOffset.FromUnixTimeSeconds(registrationDateTimestamp);
                TimeSpan onlineTime = TimeSpan.FromSeconds(onlineTimeSeconds);

                attributes = new UserAttributes(id, name, email, lastLogin.UtcDateTime, onlineTime, registrationDate.UtcDateTime);
            }

            var args = new UserAttributesEventArgs(attributes);
            OnUserAttributes(this, args);
        }

        private void OnTeleportNative(IntPtr sender)
        {
            if (OnTeleport == null)
                return;
            Teleport teleport;
            lock (this)
            {
                teleport = new Teleport
                    {
                        Avatar = GetAvatar(Functions.vp_int(sender, IntegerAttribute.AvatarSession)),
                        Position = new Vector3
                            {
                                X = Functions.vp_double(sender, FloatAttribute.TeleportX),
                                Y = Functions.vp_double(sender, FloatAttribute.TeleportY),
                                Z = Functions.vp_double(sender, FloatAttribute.TeleportZ)
                            },
                        Rotation = new Vector3
                            {
                                X = Functions.vp_double(sender, FloatAttribute.TeleportPitch),
                                Y = Functions.vp_double(sender, FloatAttribute.TeleportYaw),
                                Z = 0 /* Roll not implemented yet */
                            },
                            // TODO: maintain user count and world state statistics.
                        World = new World { Name = Functions.vp_string(sender, StringAttribute.TeleportWorld),State = WorldState.Unknown, UserCount=-1 }
                    };
            }
            OnTeleport(this, new TeleportEventArgs(teleport));
        }

        private void OnGetFriendsCallbackNative(IntPtr sender, int rc, int reference)
        {
            if (OnFriendAddCallback == null)
                return;

            int userId;
            string name;
            bool isOnline;
            
            lock (this)
            {
                userId = Functions.vp_int(sender, IntegerAttribute.UserId);
                name = Functions.vp_string(sender, StringAttribute.FriendName);
                isOnline = Functions.vp_int(sender, IntegerAttribute.FriendOnline) == 1;
            }

            var friend = new Friend(userId, name, isOnline);
            var args = new FriendsGetCallbackEventArgs(friend);
            
            Debug.Assert(!(OnFriendsGetCallback is null), $"{nameof(OnFriendsGetCallback)} != null");
            OnFriendsGetCallback.Invoke(this, args);
        }

        private void OnFriendDeleteCallbackNative(IntPtr sender, int rc, int reference)
        {
            // todo: implement this.
        }

        private void OnFriendAddCallbackNative(IntPtr sender, int rc, int reference)
        {
            // todo: implement this.
        }

        private void OnChatNative(IntPtr sender)
        {
            if (OnChatMessage is null)
                return;

            Avatar avatar;
            ChatMessage message;
            
            lock (this)
            {
                int session = Functions.vp_int(sender, IntegerAttribute.AvatarSession);
                var type = (ChatMessageTypes) Functions.vp_int(sender, IntegerAttribute.ChatType);
                var effects = (TextEffectTypes) Functions.vp_int(sender, IntegerAttribute.ChatEffects);
                string text = Functions.vp_string(sender, StringAttribute.ChatMessage);
                string name = Functions.vp_string(sender, StringAttribute.AvatarName);
                Color color = new Color(0, 0, 0);
                
                if (type == ChatMessageTypes.Console)
                {
                    byte r = (byte)Functions.vp_int(sender, IntegerAttribute.ChatColorRed);
                    byte g = (byte)Functions.vp_int(sender, IntegerAttribute.ChatColorGreen);
                    byte b = (byte)Functions.vp_int(sender, IntegerAttribute.ChatColorBlue);

                    color = new Color(r, g, b);
                }
                
                if (!_avatars.TryGetValue(session, out avatar))
                    _avatars.Add(session, avatar = new Avatar(0, session, name, 0, Vector3.Zero, Vector3.Zero, DateTimeOffset.Now, string.Empty, string.Empty));
                
                message = new ChatMessage(name, text, type, color, effects);
            }

            var args = new ChatMessageEventArgs(avatar, message);
            OnChatMessage.Invoke(this, args);
        }

        private void OnAvatarAddNative(IntPtr sender)
        {
            Avatar avatar;
            
            lock (this)
            {
                int session = Functions.vp_int(sender, IntegerAttribute.AvatarSession);
                int userId = Functions.vp_int(sender, IntegerAttribute.UserId);
                int type = Functions.vp_int(sender, IntegerAttribute.AvatarType);
                string name = Functions.vp_string(sender, StringAttribute.AvatarName);

                double x = Functions.vp_double(sender, FloatAttribute.AvatarX);
                double y = Functions.vp_double(sender, FloatAttribute.AvatarY);
                double z = Functions.vp_double(sender, FloatAttribute.AvatarZ);
                
                double pitch = Functions.vp_double(sender, FloatAttribute.AvatarPitch);
                double yaw = Functions.vp_double(sender, FloatAttribute.AvatarYaw);

                string applicationName = Functions.vp_string(sender, StringAttribute.ApplicationName);
                string applicationVersion = Functions.vp_string(sender, StringAttribute.ApplicationVersion);

                var position = new Vector3(x, y, z);
                var rotation = new Vector3(pitch, yaw, 0);

                avatar = new Avatar(userId, 0, name, type, position, rotation, DateTimeOffset.Now, applicationName, applicationVersion);

                if (_avatars.ContainsKey(session))
                    _avatars[session] = avatar;
                else
                    _avatars.Add(session, avatar);
            }
            
            if (OnAvatarEnter is null) return;

            var args = new AvatarEnterEventArgs(avatar);
            OnAvatarEnter?.Invoke(this, args);
        }

        private void OnAvatarChangeNative(IntPtr sender)
        {
            Avatar avatar;
            Avatar oldAvatar = null;
            lock (this)
            {
                int session = Functions.vp_int(sender, IntegerAttribute.AvatarSession);

                if (_avatars.TryGetValue(session, out avatar))
                    oldAvatar = (Avatar) avatar.Clone();
                else
                    avatar = new Avatar();

                double x = Functions.vp_double(sender, FloatAttribute.AvatarX);
                double y = Functions.vp_double(sender, FloatAttribute.AvatarY);
                double z = Functions.vp_double(sender, FloatAttribute.AvatarZ);
                
                double pitch = Functions.vp_double(sender, FloatAttribute.AvatarPitch);
                double yaw = Functions.vp_double(sender, FloatAttribute.AvatarYaw);

                avatar.Name = Functions.vp_string(sender, StringAttribute.AvatarName);
                avatar.AvatarType = Functions.vp_int(sender, IntegerAttribute.AvatarType);
                avatar.Position = new Vector3(x, y, z);
                avatar.Rotation = new Vector3(pitch, yaw, 0);
                avatar.LastChanged = DateTimeOffset.Now;
            }
            OnAvatarChange?.Invoke(this, new AvatarChangeEventArgs(avatar, oldAvatar));
        }

        private void OnAvatarDeleteNative(IntPtr sender)
        {
            Avatar avatar;
            
            lock (this)
            {
                int session = Functions.vp_int(sender, IntegerAttribute.AvatarSession);
                if (!_avatars.TryGetValue(session, out avatar))
                    return;
                
                _avatars.Remove(session);
            }
            
            OnAvatarLeave?.Invoke(this, new AvatarLeaveEventArgs(avatar));
        }

        private void OnAvatarClickNative(IntPtr sender)
        {
            if (OnAvatarClick is null)
                return;
            
            int avatarSession;
            int clickedSession;
            Vector3 hitPoint;
            
            lock (this)
            {
                avatarSession = Functions.vp_int(sender, IntegerAttribute.AvatarSession);
                clickedSession = Functions.vp_int(sender, IntegerAttribute.ClickedSession);
                
                if (clickedSession == 0)
                    clickedSession = avatarSession;

                double hitX = Functions.vp_double(sender, FloatAttribute.ClickHitX);
                double hitY = Functions.vp_double(sender, FloatAttribute.ClickHitY);
                double hitZ = Functions.vp_double(sender, FloatAttribute.ClickHitZ);
                hitPoint = new Vector3(hitX, hitY, hitZ);
            }
            
            var avatar = GetAvatar(avatarSession);
            var clickedAvatar = GetAvatar(clickedSession);
            var args = new AvatarClickEventArgs(avatar, clickedAvatar, hitPoint);
            
            Debug.Assert(!(OnAvatarClick is null), $"{nameof(OnAvatarClick)} != null");
            OnAvatarClick.Invoke(this, args);
        }

        private void OnObjectClickNative(IntPtr sender)
        {
            if (OnObjectClick is null)
                return;
            
            int session;
            int objectId;
            Vector3 hitPoint;
            
            lock (this)
            {
                session = Functions.vp_int(sender, IntegerAttribute.AvatarSession);
                objectId = Functions.vp_int(sender, IntegerAttribute.ObjectId);
                double hitX = Functions.vp_double(sender, FloatAttribute.ClickHitX);
                double hitY = Functions.vp_double(sender, FloatAttribute.ClickHitY);
                double hitZ = Functions.vp_double(sender, FloatAttribute.ClickHitZ);
                hitPoint = new Vector3(hitX, hitY, hitZ);
            }

            var avatar = GetAvatar(session);
            var vpObject = new VpObject { Id = objectId };
            var args = new ObjectClickArgs(avatar, vpObject, hitPoint);
            
            Debug.Assert(!(OnObjectClick is null), $"{nameof(OnObjectClick)} != null");
            OnObjectClick.Invoke(this, args);
        }

        private void OnObjectBumpNative(IntPtr sender)
        {
            if (OnObjectBump == null)
                return;

            int session;
            int objectId;
            
            lock (this)
            {
                session = Functions.vp_int(sender, IntegerAttribute.AvatarSession);
                objectId = Functions.vp_int(sender, IntegerAttribute.ObjectId);
            }

            var avatar = GetAvatar(session);
            var vpObject = new VpObject { Id = objectId };
            var args = new ObjectBumpArgs(avatar, vpObject, BumpType.BumpBegin);

            Debug.Assert(!(OnObjectBump is null), $"{nameof(OnObjectBump)} != null");
            OnObjectBump.Invoke(this, args);
        }

        private void OnObjectBumpEndNative(IntPtr sender)
        {
            if (OnObjectBump == null)
                return;
            
            int session;
            int objectId;
            
            lock (this)
            {
                session = Functions.vp_int(sender, IntegerAttribute.AvatarSession);
                objectId = Functions.vp_int(sender, IntegerAttribute.ObjectId);
            }

            var avatar = GetAvatar(session);
            var vpObject = new VpObject { Id = objectId };
            var args = new ObjectBumpArgs(avatar, vpObject, BumpType.BumpEnd);

            Debug.Assert(!(OnObjectBump is null), $"{nameof(OnObjectBump)} != null");
            OnObjectBump.Invoke(this, args);
        }

        private void OnObjectDeleteNative(IntPtr sender)
        {
            if (OnObjectDelete == null)
                return;
            
            int session;
            int objectId;
            
            lock (this)
            {
                session = Functions.vp_int(sender, IntegerAttribute.AvatarSession);
                objectId = Functions.vp_int(sender, IntegerAttribute.ObjectId);
            }

            var avatar = GetAvatar(session);
            var vpObject = new VpObject { Id = objectId };
            var args = new ObjectDeleteArgs(avatar, vpObject);
            
            Debug.Assert(!(OnObjectDelete is null), $"{nameof(OnObjectDelete)} != null");
            OnObjectDelete.Invoke(this, args);
        }

        private void OnObjectCreateNative(IntPtr sender)
        {
            if (OnObjectCreate is null && OnQueryCellResult is null)
                return;
            
            int session;
            
            lock (this)
            {
                session = Functions.vp_int(sender, IntegerAttribute.AvatarSession);
            }
            
            var avatar = GetAvatar(session);
            
            GetVpObject(sender, out VpObject vpObject);
            
            if (session == 0)
                OnQueryCellResult?.Invoke(this, new QueryCellResultArgs(vpObject));
            else
                OnObjectCreate?.Invoke(this, new ObjectCreateArgs(avatar, vpObject));
        }

        public Avatar GetAvatar(int session)
        {
            _avatars.TryGetValue(session, out Avatar avatar);
            return avatar;
        }

        private static void GetVpObject(IntPtr sender, out VpObject vpObject)
        {
            vpObject = new VpObject
            {
                Action = Functions.vp_string(sender, StringAttribute.ObjectAction),
                Description = Functions.vp_string(sender, StringAttribute.ObjectDescription),
                Id = Functions.vp_int(sender, IntegerAttribute.ObjectId),
                Model = Functions.vp_string(sender, StringAttribute.ObjectModel),
                Data = Functions.GetData(sender, DataAttribute.ObjectData),

                Rotation = new Vector3
                {
                    X = Functions.vp_double(sender, FloatAttribute.ObjectRotationX),
                    Y = Functions.vp_double(sender, FloatAttribute.ObjectRotationY),
                    Z = Functions.vp_double(sender, FloatAttribute.ObjectRotationZ)
                },

                Time = DateTimeOffset.FromUnixTimeSeconds(Functions.vp_int(sender, IntegerAttribute.ObjectTime)).UtcDateTime,
                ObjectType = Functions.vp_int(sender, IntegerAttribute.ObjectType),
                Owner = Functions.vp_int(sender, IntegerAttribute.ObjectUserId),
                Position = new Vector3
                {
                    X = Functions.vp_double(sender, FloatAttribute.ObjectX),
                    Y = Functions.vp_double(sender, FloatAttribute.ObjectY),
                    Z = Functions.vp_double(sender, FloatAttribute.ObjectZ)
                },
                Angle = Functions.vp_double(sender, FloatAttribute.ObjectRotationAngle)
            };
        }

        private void OnObjectChangeNative(IntPtr sender)
        {
            if (OnObjectChange == null) return;
            VpObject vpObject;
            int sessionId;
            lock (this)
            {
                GetVpObject(sender, out vpObject);
                sessionId = Functions.vp_int(sender, IntegerAttribute.AvatarSession);
            }
            OnObjectChange(this, new ObjectChangeArgs(GetAvatar(sessionId), vpObject));
        }

        private void OnQueryCellEndNative(IntPtr sender)
        {
            if (OnQueryCellEnd == null) return;
            int x;
            int z;
            lock (this)
            {
                x = Functions.vp_int(sender, IntegerAttribute.CellX);
                z = Functions.vp_int(sender, IntegerAttribute.CellZ);
            }
            OnQueryCellEnd(this, new QueryCellEndArgs(new Cell(x, z)));
        }

        private void OnWorldListNative(IntPtr sender)
        {
            if (OnWorldList == null)
                return;

            World data;
            lock (this)
            {
                string worldName = Functions.vp_string(_instance, StringAttribute.WorldName);
                data = new World()
                {
                    Name = worldName,
                    State = (WorldState)Functions.vp_int(_instance, IntegerAttribute.WorldState),
                    UserCount = Functions.vp_int(_instance, IntegerAttribute.WorldUsers)
                };
            }
            if (_worlds.ContainsKey(data.Name))
                _worlds.Remove(data.Name);
            _worlds.Add(data.Name,data);
            OnWorldList(this, new WorldListEventArgs(data));
        }

        private void OnWorldSettingNativeEvent(IntPtr instance)
        {
            if (!_worlds.ContainsKey(Configuration.World.Name))
            {
                _worlds.Add(Configuration.World.Name,Configuration.World);
            }
            var world = _worlds[Configuration.World.Name];
            var key = Functions.vp_string(instance, StringAttribute.WorldSettingKey);
            var value = Functions.vp_string(instance, StringAttribute.WorldSettingValue);
            world.RawAttributes[key] = value;
        }

        private void OnWorldSettingsChangedNativeEvent(IntPtr instance)
        {
            // Initialize World Object Cache if a local object path has been specified and a objectpath is speficied in the world attributes.
            // TODO: some world, such as Test do not specify a objectpath, maybe there's a default search path we dont know of.
            var world = _worlds[Configuration.World.Name];

            OnWorldSettingsChanged?.Invoke(this, new WorldSettingsChangedEventArgs(_worlds[Configuration.World.Name]));
        }

        private void OnUniverseDisconnectNative(IntPtr sender)
        {
            if (OnUniverseDisconnect == null) return;
            OnUniverseDisconnect(this, new UniverseDisconnectEventArgs(Universe));
        }

        private void OnWorldDisconnectNative(IntPtr sender)
        {
            if (OnWorldDisconnect == null) return;
            OnWorldDisconnect(this, new WorldDisconnectEventArgs(World));
        }

        private void OnJoinNative(IntPtr sender)
        {
            if (OnJoin == null) return;

            lock (this)
            {
                int userId = Functions.vp_int(sender, IntegerAttribute.UserId);
                string name = Functions.vp_string(sender, StringAttribute.JoinName);

                var avatar = new Avatar(userId, -1, name, -1, Vector3.Zero, Vector3.Zero, DateTimeOffset.MinValue, string.Empty, string.Empty);
                var args = new JoinEventArgs(avatar);
                OnJoin?.Invoke(this, args);
            }
        }

        #endregion

        #region Cleanup

        public void ReleaseEvents()
        {
            lock (this)
            {
                OnChatMessage = null;
                OnAvatarEnter = null;
                OnAvatarChange = null;
                OnAvatarLeave = null;
                OnObjectCreate = null;
                OnObjectChange = null;
                OnObjectDelete = null;
                OnObjectClick = null;
                OnObjectBump = null;
                OnWorldList = null;
                OnWorldDisconnect = null;
                OnWorldSettingsChanged = null;
                OnWorldDisconnect = null;
                OnUniverseDisconnect = null;
                OnUserAttributes = null;
                OnQueryCellResult = null;
                OnQueryCellEnd = null;
                OnFriendAddCallback = null;
                OnFriendsGetCallback = null;
            }
        }

        public void Dispose()
        {
            if (_instance != IntPtr.Zero)
            {
                Functions.vp_destroy(_instance);
            }
            
            if (_instanceHandle != GCHandle.FromIntPtr(IntPtr.Zero))
            {
                _instanceHandle.Free();
            }

            GC.SuppressFinalize(this);
        }

        #endregion

        #region Friend Functions

        public void GetFriends()
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_friends_get(_instance));
            }
        }

        public void AddFriendByName(Friend friend)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_friend_add_by_name(_instance, friend.Name));
            }
        }

        public void AddFriendByName(string name)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_friend_add_by_name(_instance, name));
            }
        }

        public void DeleteFriendById(int friendId)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_friend_delete(_instance, friendId));
            }
        }

        public void DeleteFriendById(Friend friend)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_friend_delete(_instance, friend.UserId));
            }
        }

        #endregion

        #region ITerrainFunctions Implementation

        public void TerrianQuery(int tileX, int tileZ, int[,] nodes)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_terrain_query(_instance, tileX, tileZ, nodes));
            }
        }

        public void SetTerrainNode(int tileX, int tileZ, int nodeX, int nodeZ, TerrainCell[,] cells)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_terrain_node_set(_instance, tileX, tileZ, nodeX, nodeZ, cells));
            }
        }

        #endregion

        #region Implementation of IInstanceEvents
        
        #endregion


    }
}
