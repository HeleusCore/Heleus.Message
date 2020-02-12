using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Heleus.Apps.Message;
using Heleus.Cryptography;
using Heleus.MessageService;

namespace Heleus.Apps.Shared
{
    public class MessageApp : AppBase<MessageApp>
    {
        readonly Dictionary<string, MessageNode> _nodes = new Dictionary<string, MessageNode>();

        public MessageNode GetNode(ServiceNode serviceNode)
        {
            if (serviceNode == null)
                return null;

            if (serviceNode.Active)
            {
                if (serviceNode.HasUnlockedServiceAccount && serviceNode.Active)
                {
                    if (!_nodes.TryGetValue(serviceNode.Id, out var node))
                    {
                        node = (MessageNode)Activator.CreateInstance(typeof(MessageNode), serviceNode);// new MessageNode(serviceNode);
                        _nodes[serviceNode.Id] = node;
                    }

                    return node;
                }
            }

            return null;
        }

        public List<MessageNode> GetAllNodes()
        {
            var result = new List<MessageNode>();

            foreach (var serviceNode in ServiceNodeManager.Current.ServiceNodes)
            {
                var node = GetNode(serviceNode);
                if (node != null)
                    result.Add(node);
            }

            return result;
        }

        public List<Chat> GetAllChats()
        {
            var result = new List<Chat>();

            var nodes = GetAllNodes();
            foreach(var node in nodes)
            {
                result.AddRange(node.Chats);
            }

            result.Sort((a, b) => b.LastTimestamp.CompareTo(a.LastTimestamp));

            return result;
        }

        protected override async Task ServiceNodesLoaded(ServiceNodesLoadedEvent arg)
        {
            await base.ServiceNodesLoaded(arg);

            var nodes = GetAllNodes();
            foreach (var node in nodes)
                node.Init();

            await UIApp.Current.SetFinishedLoading();
        }

        public override void UpdateSubmitAccounts()
        {
            if (!ServiceNodeManager.Current.Ready)
                return;

            var index = MessageServiceInfo.SubmitAccountIndex;

            foreach (var serviceNode in ServiceNodeManager.Current.ServiceNodes)
            {
                foreach (var serviceAccount in serviceNode.ServiceAccounts.Values)
                {
                    var keyIndex = serviceAccount.KeyIndex;

                    if (!serviceNode.HasSubmitAccount(keyIndex, index))
                    {
                        serviceNode.AddSubmitAccount(new SubmitAccount(serviceNode, keyIndex, index, false));
                    }
                }
            }
        }

        public override ServiceNode GetLastUsedServiceNode(string key = "default")
        {
            var node = base.GetLastUsedServiceNode(key);
            if (node != null)
                return node;

            return ServiceNodeManager.Current.FirstServiceNode;
        }

        public override T GetLastUsedSubmitAccount<T>(string key = "default")
        {
            var account = base.GetLastUsedSubmitAccount<T>(key);
            if (account != null)
                return account;

            return null;
        }
    }
}