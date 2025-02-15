﻿// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.AI.Embeddings;

/// <summary>
/// Represents a generator of text embeddings of type <c>float</c>.
/// </summary>
public interface ITextEmbeddingGeneration : IEmbeddingGeneration<string, float>
{
}
