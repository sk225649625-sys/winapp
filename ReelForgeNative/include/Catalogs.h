#pragma once
#include <string>
#include <vector>

struct CatalogItem
{
    std::wstring key;
    std::wstring label;
};

namespace Catalogs
{
    const std::vector<CatalogItem>& Motions();
    const std::vector<CatalogItem>& Transitions();
    const std::vector<CatalogItem>& Looks();
    const std::vector<CatalogItem>& Captions();
    const std::vector<CatalogItem>& Fits();
    const std::vector<CatalogItem>& AudioFx();

    const std::vector<std::wstring>& DefaultMotions();
    const std::vector<std::wstring>& DefaultTransitions();
}
