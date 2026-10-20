using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Branching;


namespace DialogueSystem.Editor
{
    public class DialogueNodeView : Node
    {
        public DialogueNode NodeData => nodeData;

        public DialogueNode nodeData;
        public Port inputPort;
        public Port nextPort;

        public DialogueNodeView(DialogueNode nodeData)
        {
            this.nodeData = nodeData;
            this.title = nodeData.nodeID;
            this.viewDataKey = nodeData.nodeID;

            CreateInputPort();
            CreateOutputPort();
            CreateTextFields();

            for (int i = 0; i < nodeData.choices.Count; i++)
            {
                CreateChoiceView(nodeData.choices[i]);
                AddChoicePort(nodeData.choices[i], i);
            }

            RefreshExpandedState();
            RefreshPorts();
            UpdateNextPortVisibility();
        }

        private void CreateInputPort()
        {
            inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            inputPort.portName = "In";
            inputContainer.Add(inputPort);
        }

        private void CreateOutputPort()
        {
            nextPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            nextPort.portName = "Next";
            outputContainer.Add(nextPort);
        }

        private void CreateTextFields()
        {
            // Node ID
            var idField = new TextField("Node ID") { value = nodeData.nodeID };
            idField.RegisterValueChangedCallback(evt =>
            {
                nodeData.nodeID = evt.newValue;
                this.title = evt.newValue;
                this.viewDataKey = evt.newValue;
            });
            PreventInterference(idField);
            extensionContainer.Add(idField);

            // 話者名
            var speakerField = new TextField("Speaker") { value = nodeData.speakerName };
            speakerField.RegisterValueChangedCallback(evt => nodeData.speakerName = evt.newValue);
            PreventInterference(speakerField);
            extensionContainer.Add(speakerField);

            // 本文
            var dialogueTextField = new TextField("Dialogue Text")
            {
                value = nodeData.dialogueText,
                multiline = true
            };
            dialogueTextField.RegisterValueChangedCallback(evt => nodeData.dialogueText = evt.newValue);
            PreventInterference(dialogueTextField);
            extensionContainer.Add(dialogueTextField);

            // 選択肢追加ボタン
            var addChoiceButton = new Button(() =>
            {
                var newChoice = new ChoiceData
                {
                    choiceText = "New Choice",
                    branchType = "DefaultChoiceHandler", // クラス名に統一
                    conditionKey = "",
                    conditionValue = 0
                };
                nodeData.choices.Add(newChoice);
                int choiceIndex = nodeData.choices.Count - 1;
                CreateChoiceView(newChoice);
                AddChoicePort(newChoice, choiceIndex);
                RefreshExpandedState();
            })
            { text = "Add Choice" };
            extensionContainer.Add(addChoiceButton);
        }

        private void CreateChoiceView(ChoiceData choice)
        {
            var choiceContainer = new VisualElement();
            choiceContainer.style.flexDirection = FlexDirection.Row;
            choiceContainer.style.alignItems = Align.Center;
            choiceContainer.style.marginBottom = 2;

            // 1. 選択肢テキスト
            var choiceTextField = new TextField { value = choice.choiceText };
            choiceTextField.style.flexGrow = 1;
            choiceTextField.style.minWidth = 60;
            choiceTextField.RegisterValueChangedCallback(evt =>
            {
                choice.choiceText = evt.newValue;
                int choiceIndex = nodeData.choices.IndexOf(choice);
                if (choiceIndex >= 0)
                {
                    var ports = outputContainer.Children().OfType<Port>().Where(p => p.userData is int).ToList();
                    var port = ports.FirstOrDefault(p => (int)p.userData == choiceIndex);
                    if (port != null) port.portName = evt.newValue;
                }
            });
            PreventInterference(choiceTextField);
            choiceContainer.Add(choiceTextField);

            // IDialogueBranchHandlerを継承しているすべてのクラスを自動検知
            var handlerTypes = UnityEditor.TypeCache.GetTypesDerivedFrom<IDialogueBranchHandler>();

            // 重複していた旧文字列（DefaultChoice, AutoBranch, SpecialUI）を排除し、実際のクラス名のみをリスト化
            List<string> branchOptions = new List<string>();
            foreach (var t in handlerTypes)
            {
                if (!t.IsAbstract && !t.IsInterface && !branchOptions.Contains(t.Name))
                {
                    branchOptions.Add(t.Name); // ハンドラーのクラス名のみを追加 (DefaultChoiceHandler 等)
                }
            }

            // 旧データ（"DefaultChoice"等）が残っていた場合の移行互換サポート
            if (choice.branchType == "DefaultChoice") choice.branchType = "DefaultChoiceHandler";
            if (choice.branchType == "AutoBranch") choice.branchType = "AutoBranchHandler";

            // リストが空だった場合のフォールバック
            if (branchOptions.Count == 0) branchOptions.Add("DefaultChoiceHandler");

            // ドロップダウンフィールドを作成
            string currentValue = branchOptions.Contains(choice.branchType) ? choice.branchType : branchOptions[0];
            choice.branchType = currentValue;

            var branchField = new DropdownField(branchOptions, currentValue);
            branchField.style.width = 150;
            branchField.RegisterValueChangedCallback(evt => choice.branchType = evt.newValue);
            choiceContainer.Add(branchField);

            // 3. Key
            var keyLabel = new Label("Key:");
            keyLabel.style.fontSize = 10;
            keyLabel.style.marginLeft = 4;
            choiceContainer.Add(keyLabel);

            var keyField = new TextField { value = choice.conditionKey };
            keyField.style.width = 65;
            keyField.RegisterValueChangedCallback(evt => choice.conditionKey = evt.newValue);
            PreventInterference(keyField);
            choiceContainer.Add(keyField);

            // 演算子 (Operator)
            var opField = new EnumField(choice.conditionOperator);
            opField.style.width = 40;
            opField.RegisterValueChangedCallback(evt => choice.conditionOperator = (ConditionOperator)evt.newValue);
            PreventInterference(opField);
            choiceContainer.Add(opField);

            // 4. Val
            var valLabel = new Label("Val:");
            valLabel.style.fontSize = 10;
            valLabel.style.marginLeft = 4;
            choiceContainer.Add(valLabel);

            var valField = new IntegerField { value = choice.conditionValue };
            valField.style.width = 45;
            valField.RegisterValueChangedCallback(evt => choice.conditionValue = evt.newValue);
            PreventInterference(valField);
            choiceContainer.Add(valField);

            // 5. 削除ボタン
            var deleteButton = new Button(() =>
            {
                int index = nodeData.choices.IndexOf(choice);
                if (index >= 0)
                {
                    nodeData.choices.RemoveAt(index);
                    var ports = outputContainer.Children().OfType<Port>().Where(p => p.userData is int).ToList();
                    var targetPort = ports.FirstOrDefault(p => (int)p.userData == index);
                    if (targetPort != null)
                    {
                        var edges = targetPort.connections.ToList();
                        foreach (var edge in edges) edge.input?.Disconnect(edge);
                        outputContainer.Remove(targetPort);
                    }
                    var remainingPorts = outputContainer.Children().OfType<Port>().Where(p => p.userData is int).ToList();
                    for (int i = 0; i < remainingPorts.Count; i++)
                    {
                        remainingPorts[i].userData = i;
                        if (i < nodeData.choices.Count) remainingPorts[i].portName = nodeData.choices[i].choiceText;
                    }
                }
                extensionContainer.Remove(choiceContainer);
                RefreshPorts();
                RefreshExpandedState();
                UpdateNextPortVisibility();
            })
            { text = "X" };
            choiceContainer.Add(deleteButton);

            extensionContainer.Add(choiceContainer);
        }

        public void AddChoicePort(ChoiceData choice, int choiceIndex)
        {
            var generatedPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            generatedPort.portName = choice.choiceText;
            generatedPort.userData = choiceIndex;
            outputContainer.Add(generatedPort);
            RefreshPorts();
            RefreshExpandedState();
            UpdateNextPortVisibility();
        }

        private void UpdateNextPortVisibility()
        {
            if (nextPort != null)
            {
                nextPort.style.display = nodeData.choices.Count > 0 ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        // 入力割り込み防止用共通メソッド
        private void PreventInterference(VisualElement element)
        {
            element.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
            element.RegisterCallback<KeyDownEvent>(evt => evt.StopPropagation());
        }
    }
}