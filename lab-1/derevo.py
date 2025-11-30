from Pyro4 import expose
import math


class Solver:
    def __init__(self, workers=None, input_file_name=None, output_file_name=None):
        self.input_file_name = input_file_name
        self.output_file_name = output_file_name
        self.workers = workers

    def solve(self):
        n = self.read_input()
        sqrt_n = int(math.sqrt(n)) + 1
        small_primes = self.sequential_sieve(sqrt_n)

        work_range = n - sqrt_n
        step = work_range // len(self.workers)

        mapped = []
        for i in range(len(self.workers)):
            start = sqrt_n + 1 + i * step
            end = sqrt_n + 1 + (i + 1) * step if i < len(self.workers) - 1 else n
            mapped.append(self.workers[i].mymap(start, end, small_primes))

        all_primes = self.myreduce(mapped, small_primes)
        self.write_output(all_primes)

    @staticmethod
    @expose
    def sequential_sieve(limit):
        if limit < 2:
            return []

        is_prime = [True] * (limit + 1)
        is_prime[0] = is_prime[1] = False

        for i in range(2, int(math.sqrt(limit)) + 1):
            if is_prime[i]:
                for j in range(i * i, limit + 1, i):
                    is_prime[j] = False

        return [i for i in range(2, limit + 1) if is_prime[i]]

    @staticmethod
    @expose
    def mymap(start, end, small_primes):
        size = end - start + 1
        is_prime = [True] * size

        for prime in small_primes:
            first_multiple = ((start + prime - 1) // prime) * prime
            if first_multiple == prime:
                first_multiple += prime

            current = first_multiple
            while current <= end:
                is_prime[current - start] = False
                current += prime

        primes = []
        for i in range(size):
            num = start + i
            if is_prime[i] and num > 1:
                primes.append(num)

        return primes

    @staticmethod
    @expose
    def myreduce(mapped, small_primes):
        all_primes = small_primes[:]

        for result in mapped:
            all_primes.extend(result.value)

        all_primes.sort()
        return all_primes

    def read_input(self):
        with open(self.input_file_name, 'r') as f:
            n = int(f.readline().strip())
        return n

    def write_output(self, primes):
        with open(self.output_file_name, 'w') as f:
            f.write(', '.join(map(str, primes)))
            f.write('\n')