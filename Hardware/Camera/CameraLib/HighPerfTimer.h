#pragma once
#include<chrono>

class HighPerfTimer
{
private:
	std::chrono::high_resolution_clock::time_point _start;
	std::chrono::high_resolution_clock::time_point _end;

	inline int64_t Elapsed_ns()
	{
		int64_t fp_ns = std::chrono::duration_cast<std::chrono::nanoseconds>(_end - _start).count();
		return fp_ns;
	}
public:
	HighPerfTimer()
	{
		
	}

	void Start()
	{
		_start = std::chrono::high_resolution_clock::now();
	}

	void Stop()
	{
		_end = std::chrono::high_resolution_clock::now();
	}

	double Elapsed_us()
	{
		return Elapsed_ns() / 1e3;
	}

	double Elapsed_ms()
	{
		return Elapsed_ns() / 1e6;
	}

	double Elapsed_s()
	{
		return Elapsed_ns() / 1e9;
	}
};
