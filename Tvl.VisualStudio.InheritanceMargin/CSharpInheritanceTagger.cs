﻿namespace Tvl.VisualStudio.InheritanceMargin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Tagging;
    using _DTE = EnvDTE._DTE;
    using DTE = EnvDTE.DTE;

    internal class CSharpInheritanceTagger : ITagger<IInheritanceTag>
    {
        internal static readonly ITagSpan<IInheritanceTag>[] NoTags = new ITagSpan<IInheritanceTag>[0];
        private static Type _analyzerType;

        private readonly CSharpInheritanceTaggerProvider _provider;
        private readonly ITextBuffer _buffer;
        private readonly IInheritanceParser _analyzer;

        private ITagSpan<IInheritanceTag>[] _tags = NoTags;

        public CSharpInheritanceTagger(CSharpInheritanceTaggerProvider provider, ITextBuffer buffer)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            this._provider = provider;
            this._buffer = buffer;

            if (_analyzerType == null)
                _analyzerType = LoadAnalyzerType(provider.GlobalServiceProvider);

            this._analyzer = (IInheritanceParser)Activator.CreateInstance(_analyzerType, buffer, provider.TaskScheduler, provider.TextDocumentFactoryService, provider.OutputWindowService, provider.GlobalServiceProvider, new InheritanceTagFactory());
            this._analyzer.ParseComplete += HandleParseComplete;
            this._analyzer.RequestParse(false);
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        internal static CSharpInheritanceTagger CreateInstance(CSharpInheritanceTaggerProvider provider, ITextBuffer buffer)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            return buffer.Properties.GetOrCreateSingletonProperty(() => new CSharpInheritanceTagger(provider, buffer));
        }

        private static Type LoadAnalyzerType(SVsServiceProvider serviceProvider)
        {
            Version version;
            int vsMajorVersion;
            if (Version.TryParse(((DTE)serviceProvider.GetService(typeof(_DTE))).Version, out version))
            {
                vsMajorVersion = version.Major;
            }
            else
            {
                vsMajorVersion = 11;
            }

            bool vs2010 = vsMajorVersion == 10;
            bool vs2012 = vsMajorVersion == 11;
            bool vs14 = vsMajorVersion == 14;

            string assemblyName;
            if (vs2010)
                assemblyName = "Tvl.VisualStudio.InheritanceMargin.CSharp.10.0";
            else if (vs2012)
                assemblyName = "Tvl.VisualStudio.InheritanceMargin.CSharp.11.0";
            else if (vs14)
                assemblyName = "Tvl.VisualStudio.InheritanceMargin.CSharp.14.0";
            else if (RoslynUtilities.IsRoslynInstalled(serviceProvider))
                assemblyName = "Tvl.VisualStudio.InheritanceMargin.CSharp.Roslyn";
            else
                assemblyName = "Tvl.VisualStudio.InheritanceMargin.CSharp.12.0";

            Assembly assembly = Assembly.Load(assemblyName);
            return assembly.GetType("Tvl.VisualStudio.InheritanceMargin.CSharp.CSharpInheritanceAnalyzer");
        }

        public IEnumerable<ITagSpan<IInheritanceTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            return _tags;
        }

        protected virtual void HandleParseComplete(object sender, InheritanceParseResultEventArgs e)
        {
            IEnumerable<ITagSpan<IInheritanceTag>> tags = NoTags;

            InheritanceParseResultEventArgs ie = e as InheritanceParseResultEventArgs;
            if (ie != null)
            {
                tags = ie.Tags;
            }

            _tags = tags.ToArray();
            OnTagsChanged(new SnapshotSpanEventArgs(new SnapshotSpan(e.Snapshot, new Span(0, e.Snapshot.Length))));
        }

        protected virtual void OnTagsChanged(SnapshotSpanEventArgs e)
        {
            var t = TagsChanged;
            if (t != null)
                t(this, e);
        }
    }
}
