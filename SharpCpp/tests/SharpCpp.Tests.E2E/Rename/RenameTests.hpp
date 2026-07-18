#pragma once

struct [[sharpcpp::rename("RenamedStruct")]] original_struct
{
    original_struct()
        : value_(-1)
    {
    }

    int get_value() const
    {
        return value_;
    }

    int value_;
};

inline original_struct* create_original_struct()
{
    return new original_struct();
}

template <typename T>
struct [[sharpcpp::rename("ContainerOf<{0}>")]] item_holder
{
    explicit item_holder(T item)
        : item_(item)
    {
    }

    T get_item() const
    {
        return item_;
    }

    static item_holder<T>* create(T item)
    {
        return new item_holder<T>(item);
    }

private:
    T item_{};
};

inline item_holder<int>* make_int_holder(int value)
{
    return item_holder<int>::create(value);
}

enum class [[sharpcpp::rename("DayOfWeek")]] weekday
{
    monday = 1,
    tuesday = 2,
    wednesday = 3
};

[[sharpcpp::rename("AddValues")]]
inline int do_add(int a, int b)
{
    return a + b;
}

struct api_object
{
    api_object()
        : count_(0)
    {
    }

    [[sharpcpp::rename("GetCount")]]
    int fetch_count() const
    {
        return count_;
    }

    [[sharpcpp::rename("IncrementBy")]]
    void add(int value)
    {
        count_ += value;
    }

    [[sharpcpp::rename("MakeDefault")]]
    static api_object* create_default()
    {
        return new api_object();
    }

    [[sharpcpp::rename("ItemCount")]]
    int count_;
};

inline api_object* create_api_object()
{
    return new api_object();
}

struct [[sharpcpp::rename("{Name}Wrapper")]] data_store
{
    data_store()
        : items(0)
    {
    }

    int items;
};

inline data_store* create_data_store()
{
    return new data_store();
}
