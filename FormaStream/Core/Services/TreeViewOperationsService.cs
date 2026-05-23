using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.Input;
using FormaStream.Core.Interfaces;
using FormaStream.Infrastructure.Services;
using FormaStream.Shell.ViewModels.TreeNodes;
using Microsoft.Extensions.Logging;

namespace FormaStream.Core.Services;

public class TreeViewOperationsService : ITreeViewOperationsService
{
    private readonly IFileSystemServices _fs;
    private readonly IDbRepository _dbRepository;

    private readonly ILogger<TreeViewOperationsService> _logger;

    public TreeViewOperationsService(IDbRepository dbRepository, IFileSystemServices fs,
        ILogger<TreeViewOperationsService> logger)
    {
        _dbRepository = dbRepository;
        _fs = fs;
        _logger = logger;
    }

    public async Task<AvaloniaList<TreeNode>> LoadTreeAsync(string folderPath)
    {
        var treeNodes = new AvaloniaList<TreeNode>();

        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Папка не найдена: {folderPath}");

        try
        {
            var filePaths = Directory.EnumerateFiles(folderPath)
                .Where(file =>
                    file.EndsWith(".len", StringComparison.OrdinalIgnoreCase) &&
                    !file.EndsWith("rot.len", StringComparison.OrdinalIgnoreCase) &&
                    !file.EndsWith("cdi.len", StringComparison.OrdinalIgnoreCase))
                .ToList();

            //  Парсинг файлов (асинхронно, с БД-запросами)
            var files = await _fs.FileParser.ParseFilesAsync(filePaths);

            // Бизнес-логика: группировка (синхронно)
            var variants = _fs.Variants.CreateVariants(files);
            var orders = _fs.Orders.GroupByOrder(variants);

            // Кэш всех клиентов один раз
            var clientCache = await _dbRepository.LoadClientCacheAsync();

            // Создаём узлы дерева + загружаем данные из БД
            var processed = 0;
            var progressMax = files.Count;
            var currentProgress = 0;

            foreach (var order in orders)
            {
                var clientName =
                    _dbRepository.GetClientNameFromCache(clientCache,
                        Path.GetFileNameWithoutExtension(order.Variants.First().Files.First().Filename));

                if (clientName != null)
                {
                    order.ClientName = clientName[0];
                    order.ClientNameTranslit = clientName[1];

                    foreach (var variant in order.Variants)
                    {
                        variant.ClientName = clientName[0];
                        variant.ClientNameTranslit = clientName[1];

                        foreach (var file in variant.Files)
                        {
                            file.ClientName = clientName[0];
                            file.ClientNameTranslit = clientName[1];
                        }
                    }
                }

                var orderNode = new OrderNode(order);

                foreach (var variant in order.Variants)
                {
                    var variantNode = new VariantNode(variant)
                    {
                        Parent = orderNode
                    };

                    orderNode.Children.Add(variantNode);

                    foreach (var file in variant.Files)
                    {
                        var fileNode = new FileNode(file)
                        {
                            Parent = variantNode
                        };

                        variantNode.Children.Add(fileNode);

                        currentProgress++;
                    }
                }

                treeNodes.Add(orderNode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки дерева из {FolderPath} {ExMessage}", ex.Message, folderPath);
        }

        return treeNodes;
    }


    public void ExpandAll(IEnumerable<TreeNode> roots)
    {
        foreach (var root in roots)
            SetExpandedRecursive(root, expand: true, maxLevel: int.MaxValue);
    }


    public void CollapseAll(IEnumerable<TreeNode> roots)
    {
        foreach (var root in roots)
            SetExpandedRecursive(root, expand: false, maxLevel: int.MaxValue);
    }

    public void ShowVariants(IEnumerable<TreeNode> roots)
    {
        foreach (var root in roots)
            SetExpandedRecursive(root, expand: true, maxLevel: 0);
    }

    private void SetExpandedRecursive(TreeNode node, bool expand, int maxLevel, int currentLevel = 0)
    {
        // Раскрываем узел, только если он в пределах целевого уровня
        if (currentLevel <= maxLevel)
            node.IsExpanded = expand;
        else if (!expand)
            node.IsExpanded = false; // При сворачивании закрываем всё, что глубже

        // Рекурсия в детей
        foreach (var child in node.Children)
            SetExpandedRecursive(child, expand, maxLevel, currentLevel + 1);
    }


    private void RemoveAndClean(AvaloniaList<TreeNode> rootCollection, TreeNode nodeToRemove)
    {
        // Удаляем узел из родителя
        if (nodeToRemove.Parent != null)
            nodeToRemove.Parent.Children.Remove(nodeToRemove);
        else
            rootCollection.Remove(nodeToRemove); // Корневой узел

        var current = nodeToRemove.Parent;

        // Поднимаемся вверх, удаляя пустых родителей
        while (current != null && current.Children.Count == 0)
        {
            var nextParent = current.Parent;
            if (nextParent == null)
            {
                rootCollection.Remove(current);
                break;
            }

            // Иначе удаляем текущий пустой узел из детей его родителя
            nextParent.Children.Remove(current);
            current = nextParent;
        }
    }

    public void SyncTreeAfterOperation(IEnumerable<TreeNode> roots, IEnumerable<string> changedFilePaths)
    {
        foreach (var path in changedFilePaths)
        {
            var node = FindFileNodeByPath(roots, path);

            // Передаём roots как AvaloniaList для RemoveAndClean
            if (node != null && roots is AvaloniaList<TreeNode> list)
                RemoveAndClean(list, node);
        }
    }


    // Поиск узла по файлу (рекурсивно)
    private FileNode? FindFileNodeByPath(IEnumerable<TreeNode> roots, string filePath)
    {
        // Нормализуем путь для кросс-платформенного сравнения
        var normalizedPath = Path.GetFullPath(filePath).ToLowerInvariant();

        foreach (var node in roots)
        {
            var found = FindFileNodeRecursive(node, normalizedPath);

            if (found != null) return found;
        }

        return null;
    }

    private FileNode? FindFileNodeRecursive(TreeNode node, string normalizedPath)
    {
        if (node is FileNode fn && fn.SourceData != null)
        {
            var nodePath = Path.GetFullPath(fn.SourceData.Filename).ToLowerInvariant();
            if (nodePath == normalizedPath) return fn;
        }

        foreach (var child in node.Children)
        {
            var found = FindFileNodeRecursive(child, normalizedPath);
            if (found != null) return found;
        }

        return null;
    }
}