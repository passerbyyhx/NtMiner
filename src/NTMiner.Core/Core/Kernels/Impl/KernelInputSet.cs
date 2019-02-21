﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace NTMiner.Core.Kernels.Impl {
    public class KernelInputSet : IKernelInputSet {
        private readonly Dictionary<Guid, KernelInputData> _dicById = new Dictionary<Guid, KernelInputData>();

        private readonly INTMinerRoot _root;

        public KernelInputSet(INTMinerRoot root) {
            _root = root;
            VirtualRoot.Accept<RefreshKernelInputSetCommand>(
                Guid.Parse("AD17471F-02D2-4ABF-B3AE-B66F7BD16FA4"),
                "处理刷新内核输入数据集命令",
                LogEnum.Console,
                action: message => {
                    var repository = NTMinerRoot.CreateServerRepository<KernelInputData>();
                    foreach (var item in repository.GetAll()) {
                        if (_dicById.ContainsKey(item.Id)) {
                            VirtualRoot.Execute(new UpdateKernelInputCommand(item));
                        }
                        else {
                            VirtualRoot.Execute(new AddKernelInputCommand(item));
                        }
                    }
                });
            VirtualRoot.Accept<AddKernelInputCommand>(
                Guid.Parse("62D0B345-26F8-42BA-B7CD-E547C2B298C9"),
                "添加内核输入组",
                LogEnum.Console,
                action: (message) => {
                    InitOnece();
                    if (message == null || message.Input == null || message.Input.GetId() == Guid.Empty) {
                        throw new ArgumentNullException();
                    }
                    if (_dicById.ContainsKey(message.Input.GetId())) {
                        return;
                    }
                    KernelInputData entity = new KernelInputData().Update(message.Input);
                    _dicById.Add(entity.Id, entity);
                    var repository = NTMinerRoot.CreateServerRepository<KernelInputData>();
                    repository.Add(entity);

                    VirtualRoot.Happened(new KernelInputAddedEvent(entity));
                });
            VirtualRoot.Accept<UpdateKernelInputCommand>(
                Guid.Parse("FED12C08-7BD7-4A8E-BD0B-A19075F4E8C4"),
                "更新内核输入组",
                LogEnum.Console,
                action: (message) => {
                    InitOnece();
                    if (message == null || message.Input == null || message.Input.GetId() == Guid.Empty) {
                        throw new ArgumentNullException();
                    }
                    if (string.IsNullOrEmpty(message.Input.Name)) {
                        throw new ValidationException("KernelInput name can't be null or empty");
                    }
                    if (!_dicById.ContainsKey(message.Input.GetId())) {
                        return;
                    }
                    KernelInputData entity = _dicById[message.Input.GetId()];
                    if (ReferenceEquals(entity, message.Input)) {
                        return;
                    }
                    entity.Update(message.Input);
                    var repository = NTMinerRoot.CreateServerRepository<KernelInputData>();
                    repository.Update(entity);

                    VirtualRoot.Happened(new KernelInputUpdatedEvent(entity));
                });
            VirtualRoot.Accept<RemoveKernelInputCommand>(
                Guid.Parse("2227F6B9-5A2A-42AB-8147-05E245E2872F"),
                "移除内核输入组",
                LogEnum.Console,
                action: (message) => {
                    InitOnece();
                    if (message == null || message.EntityId == Guid.Empty) {
                        throw new ArgumentNullException();
                    }
                    if (!_dicById.ContainsKey(message.EntityId)) {
                        return;
                    }
                    KernelInputData entity = _dicById[message.EntityId];
                    _dicById.Remove(entity.GetId());
                    var repository = NTMinerRoot.CreateServerRepository<KernelInputData>();
                    repository.Remove(message.EntityId);

                    VirtualRoot.Happened(new KernelInputRemovedEvent(entity));
                });
        }

        private bool _isInited = false;
        private object _locker = new object();

        private void InitOnece() {
            if (_isInited) {
                return;
            }
            Init();
        }

        private void Init() {
            lock (_locker) {
                if (!_isInited) {
                    var repository = NTMinerRoot.CreateServerRepository<KernelInputData>();
                    foreach (var item in repository.GetAll()) {
                        if (!_dicById.ContainsKey(item.GetId())) {
                            _dicById.Add(item.GetId(), item);
                        }
                    }
                    _isInited = true;
                }
            }
        }

        public bool Contains(Guid id) {
            InitOnece();
            return _dicById.ContainsKey(id);
        }

        public bool TryGetKernelInput(Guid id, out IKernelInput kernelInput) {
            InitOnece();
            KernelInputData data;
            var result = _dicById.TryGetValue(id, out data);
            kernelInput = data;
            return result;
        }

        public IEnumerator<IKernelInput> GetEnumerator() {
            InitOnece();
            return _dicById.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            InitOnece();
            return _dicById.Values.GetEnumerator();
        }
    }
}
