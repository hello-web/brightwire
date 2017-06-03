﻿using BrightWire.ExecutionGraph.Helper;
using MathNet.Numerics.Distributions;
using System.Collections.Generic;
using System.IO;

namespace BrightWire.ExecutionGraph.Node.Filter
{
    /// <summary>
    /// Drop out regularisation
    /// https://en.wikipedia.org/wiki/Dropout_(neural_networks)
    /// </summary>
    class DropOut : NodeBase
    {
        class Backpropagation : SingleBackpropagationBase<DropOut>
        {
            readonly IMatrix _filter;

            public Backpropagation(DropOut source, IMatrix filter) : base(source)
            {
                _filter = filter;
            }

            protected override IGraphData _Backpropagate(INode fromNode, IGraphData errorSignal, IContext context, IReadOnlyList<INode> parents)
            {
                var output = errorSignal.GetMatrix().PointwiseMultiply(_filter);
                return errorSignal.ReplaceWith(output);
            }
        }
        float _dropOutPercentage;
        Bernoulli _probabilityToDrop;

        public DropOut(float dropOutPercentage, string name = null) : base(name)
        {
            _dropOutPercentage = dropOutPercentage;
            _probabilityToDrop = new Bernoulli(_dropOutPercentage);
        }

        public override void ExecuteForward(IContext context)
        {
            if (context.IsTraining) {
                // drop out random neurons during training
                var lap = context.LinearAlgebraProvider;
                var matrix = context.Data.GetMatrix();
                var filter = lap.CreateMatrix(matrix.RowCount, matrix.ColumnCount, (i, j) => _probabilityToDrop.Sample() == 1 ? 0f : 1f);
                var output = matrix.PointwiseMultiply(filter);
                _AddNextGraphAction(context, context.Data.ReplaceWith(output), () => new Backpropagation(this, filter));
            } else {
                // otherwise scale by the drop out percentage
                var scaleFactor = 1 - _dropOutPercentage;
                var matrix = context.Data.GetMatrix();
                matrix.Multiply(scaleFactor);
                _AddNextGraphAction(context, context.Data, null);
            }
        }

        protected override void _Initalise(GraphFactory factory, string description, byte[] data)
        {
            _ReadFrom(data, reader => ReadFrom(factory, reader));
        }

        protected override (string Description, byte[] Data) _GetInfo()
        {
            return ("DO", _WriteData(WriteTo));
        }

        public override void ReadFrom(GraphFactory factory, BinaryReader reader)
        {
            _dropOutPercentage = reader.ReadSingle();
            _probabilityToDrop = new Bernoulli(_dropOutPercentage);
        }

        public override void WriteTo(BinaryWriter writer)
        {
            writer.Write(_dropOutPercentage);
        }
    }
}
