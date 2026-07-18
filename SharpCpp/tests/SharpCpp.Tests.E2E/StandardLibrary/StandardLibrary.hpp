#pragma once

#include <array>
#include <list>
#include <map>
#include <numeric>
#include <string>
#include <unordered_map>
#include <vector>

inline std::string e2e_make_std_text(const char* value)
{
    return std::string(value);
}

inline std::vector<int> e2e_make_std_numbers()
{
    return { 2, 4, 6 };
}

inline int e2e_sum_std_numbers(const std::vector<int>& values)
{
    return std::accumulate(values.begin(), values.end(), 0);
}

inline std::array<int, 4> e2e_make_std_array_numbers()
{
    return { 1, 3, 5, 7 };
}

inline int e2e_sum_std_array_numbers(const std::array<int, 4>& values)
{
    return std::accumulate(values.begin(), values.end(), 0);
}

inline std::list<std::string> e2e_make_std_words()
{
    return { "red", "green", "blue" };
}

inline int e2e_count_std_words(const std::list<std::string>& values)
{
    return static_cast<int>(values.size());
}

inline std::map<std::string, int> e2e_make_std_scores()
{
    return { { "one", 1 }, { "two", 2 } };
}

inline int e2e_total_std_scores(const std::map<std::string, int>& values)
{
    auto total = 0;
    for (const auto& [key, value] : values)
    {
        total += value;
    }

    return total;
}

inline std::unordered_map<std::string, int> e2e_make_std_lookup()
{
    return { { "left", 7 }, { "right", 9 } };
}

inline int e2e_total_std_lookup(const std::unordered_map<std::string, int>& values)
{
    auto total = 0;
    for (const auto& [key, value] : values)
    {
        total += value;
    }

    return total;
}

struct std_library_owner
{
    std_library_owner()
        : numbers({ 1, 2, 3 })
        , scores({ { "alpha", 1 }, { "beta", 2 } })
        , words({ "red", "green" })
        , lookup({ { "left", 7 }, { "right", 9 } })
    {
    }

    const std::vector<int>& get_numbers_ref() const
    {
        return numbers;
    }

    const std::map<std::string, int>& get_scores_ref() const
    {
        return scores;
    }

    const std::list<std::string>& get_words_ref() const
    {
        return words;
    }

    const std::unordered_map<std::string, int>& get_lookup_ref() const
    {
        return lookup;
    }

    std::vector<int> numbers;
    std::map<std::string, int> scores;
    std::list<std::string> words;
    std::unordered_map<std::string, int> lookup;
};
