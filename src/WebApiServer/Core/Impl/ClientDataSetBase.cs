﻿using NTMiner.Core.MinerServer;
using NTMiner.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NTMiner.Core.Impl {
    public abstract class ClientDataSetBase {
        protected readonly Dictionary<string, ClientData> _dicByObjectId = new Dictionary<string, ClientData>();
        protected readonly Dictionary<Guid, ClientData> _dicByClientId = new Dictionary<Guid, ClientData>();

        protected DateTime InitedOn = DateTime.MinValue;
        public bool IsReadied {
            get; private set;
        }

        protected abstract void DoUpdateSave(MinerData minerData);
        protected abstract void DoUpdateSave(IEnumerable<MinerData> minerDatas);
        protected abstract void DoRemoveSave(MinerData minerData);
        protected abstract void DoCheckIsOnline(IEnumerable<ClientData> clientDatas);

        private readonly bool _isPull;
        public ClientDataSetBase(bool isPull, Action<Action<IEnumerable<MinerData>>> doInit) {
            _isPull = isPull;
            doInit((minerDatas) => {
                InitedOn = DateTime.Now;
                IsReadied = true;
                foreach (var item in minerDatas) {
                    var data = ClientData.Create(item);
                    if (!_dicByObjectId.ContainsKey(item.Id)) {
                        _dicByObjectId.Add(item.Id, data);
                    }
                    if (!_dicByClientId.ContainsKey(item.ClientId)) {
                        _dicByClientId.Add(item.ClientId, data);
                    }
                }
                Write.UserOk("矿机集就绪");
                VirtualRoot.RaiseEvent(new ClientSetInitedEvent());
            });
        }

        public ClientCount ClientCount { get; private set; } = new ClientCount();

        public List<ClientData> QueryClients(
            IUser user,
            QueryClientsRequest query,
            out int total,
            out List<CoinSnapshotData> coinSnapshots,
            out int onlineCount,
            out int miningCount) {

            coinSnapshots = new List<CoinSnapshotData>();
            onlineCount = 0;
            miningCount = 0;
            if (!IsReadied) {
                total = 0;
                return new List<ClientData>();
            }
            List<ClientData> list = new List<ClientData>();
            var data = _dicByObjectId.Values.ToArray();
            for (int i = 0; i < data.Length; i++) {
                var item = data[i];
                bool isInclude = true;
                if (isInclude) {
                    if (user != null && !user.IsAdmin()) {
                        isInclude = item.LoginName == user.LoginName;
                    }
                }
                if (isInclude) {
                    if (query.GroupId.HasValue && query.GroupId.Value != Guid.Empty) {
                        isInclude = item.GroupId == query.GroupId.Value;
                    }
                }
                if (isInclude) {
                    switch (query.MineState) {
                        case MineStatus.All:
                            break;
                        case MineStatus.Mining:
                            isInclude = item.IsMining == true;
                            break;
                        case MineStatus.UnMining:
                            isInclude = item.IsMining == false;
                            break;
                        default:
                            break;
                    }
                }
                if (isInclude) {
                    if (!string.IsNullOrEmpty(query.MinerIp)) {
                        isInclude = item.MinerIp == query.MinerIp;
                    }
                }
                if (isInclude) {
                    if (!string.IsNullOrEmpty(query.MinerName)) {
                        isInclude = (!string.IsNullOrEmpty(item.MinerName) && item.MinerName.IndexOf(query.MinerName, StringComparison.OrdinalIgnoreCase) != -1)
                            || (!string.IsNullOrEmpty(item.WorkerName) && item.WorkerName.IndexOf(query.MinerName, StringComparison.OrdinalIgnoreCase) != -1);
                    }
                }
                if (isInclude) {
                    if (!string.IsNullOrEmpty(query.Version)) {
                        isInclude = !string.IsNullOrEmpty(item.Version) && item.Version.StartsWith(query.Version);
                    }
                }
                if (isInclude) {
                    if (query.WorkId.HasValue && query.WorkId.Value != Guid.Empty) {
                        isInclude = item.WorkId == query.WorkId.Value;
                    }
                    else {
                        if (!string.IsNullOrEmpty(query.Coin)) {
                            isInclude = item.MainCoinCode == query.Coin || item.DualCoinCode == query.Coin;
                        }
                        if (!string.IsNullOrEmpty(query.Pool)) {
                            isInclude = item.MainCoinPool == query.Pool || item.DualCoinPool == query.Pool;
                        }
                        if (!string.IsNullOrEmpty(query.Wallet)) {
                            isInclude = item.MainCoinWallet == query.Wallet || item.DualCoinWallet == query.Wallet;
                        }
                        if (!string.IsNullOrEmpty(query.Kernel)) {
                            isInclude = !string.IsNullOrEmpty(item.Kernel) && item.Kernel.StartsWith(query.Kernel, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
                if (isInclude) {
                    list.Add(item);
                }
            }
            total = list.Count();
            switch (query.SortDirection) {
                case SortDirection.Ascending:
                    list = list.OrderBy(a => a.MinerName).ToList();
                    break;
                case SortDirection.Descending:
                    list = list.OrderByDescending(a => a.MinerName).ToList();
                    break;
                default:
                    break;
            }
            coinSnapshots = VirtualRoot.CreateCoinSnapshots(_isPull, DateTime.Now, list, out onlineCount, out miningCount).ToList();
            var results = list.Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).ToList();
            foreach (var item in results) {
                // 去除AESPassword避免在网络上传输
                item.AESPassword = string.Empty;
            }
            DoCheckIsOnline(results);
            return results;
        }

        public ClientData GetByClientId(Guid clientId) {
            if (!IsReadied) {
                return null;
            }
            _dicByClientId.TryGetValue(clientId, out ClientData clientData);
            return clientData;
        }

        public ClientData GetByObjectId(string objectId) {
            if (!IsReadied) {
                return null;
            }
            if (objectId == null) {
                return null;
            }
            _dicByObjectId.TryGetValue(objectId, out ClientData clientData);
            return clientData;
        }

        public virtual void UpdateClient(string objectId, string propertyName, object value) {
            if (!IsReadied) {
                return;
            }
            if (objectId == null) {
                return;
            }
            if (_dicByObjectId.TryGetValue(objectId, out ClientData clientData)) {
                PropertyInfo propertyInfo = typeof(ClientData).GetProperty(propertyName);
                if (propertyInfo != null) {
                    value = VirtualRoot.ConvertValue(propertyInfo.PropertyType, value);
                    var oldValue = propertyInfo.GetValue(clientData, null);
                    if (oldValue != value) {
                        propertyInfo.SetValue(clientData, value, null);
                        DoUpdateSave(MinerData.Create(clientData));
                    }
                }
            }
        }

        public virtual void UpdateClients(string propertyName, Dictionary<string, object> values) {
            if (!IsReadied) {
                return;
            }
            if (values.Count == 0) {
                return;
            }
            PropertyInfo propertyInfo = typeof(ClientData).GetProperty(propertyName);
            if (propertyInfo != null) {
                values.ChangeValueType(propertyInfo.PropertyType);
                List<MinerData> minerDatas = new List<MinerData>();
                foreach (var kv in values) {
                    string objectId = kv.Key;
                    object value = kv.Value;
                    if (_dicByObjectId.TryGetValue(objectId, out ClientData clientData)) {
                        var oldValue = propertyInfo.GetValue(clientData, null);
                        if (oldValue != value) {
                            propertyInfo.SetValue(clientData, value, null);
                            minerDatas.Add(MinerData.Create(clientData));
                        }
                    }
                }
                DoUpdateSave(minerDatas);
            }
        }

        public void RemoveByObjectId(string objectId) {
            if (!IsReadied) {
                return;
            }
            if (objectId == null) {
                return;
            }
            if (_dicByObjectId.TryGetValue(objectId, out ClientData clientData)) {
                _dicByObjectId.Remove(objectId);
                _dicByClientId.Remove(clientData.ClientId);
                DoRemoveSave(MinerData.Create(clientData));
            }
        }

        public bool IsAnyClientInGroup(Guid groupId) {
            if (!IsReadied) {
                return false;
            }
            return _dicByObjectId.Values.Any(a => a.GroupId == groupId);
        }

        public bool IsAnyClientInWork(Guid workId) {
            if (!IsReadied) {
                return false;
            }
            return _dicByObjectId.Values.Any(a => a.WorkId == workId);
        }

        public IEnumerable<ClientData> AsEnumerable() {
            return _dicByObjectId.Values;
        }
    }
}