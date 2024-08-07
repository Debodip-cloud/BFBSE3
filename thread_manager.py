import threading
import time

class TraderThread(threading.Thread):
    def __init__(self, trader_id, trader_func, *args):
        super().__init__()
        self.trader_id = trader_id
        self.trader_func = trader_func
        self.args = args

    def run(self):
        try:
            self.trader_func(*self.args)
        except Exception as ex:
            print(f"Exception in trader thread {self.trader_id}: {ex}")

def run_trader(trader_func, *args):
    trader_func(*args)

def start_threads(traders, exchange_func, *exchange_args):
    trader_threads = []
    for tid, trader_func, trader_args in traders:
        thread = TraderThread(tid, trader_func, *trader_args)
        trader_threads.append(thread)
        thread.start()
    
    exchange_thread = threading.Thread(target=exchange_func, args=exchange_args)
    exchange_thread.start()

    return trader_threads, exchange_thread

def join_threads(trader_threads, exchange_thread):
    for thread in trader_threads:
        thread.join()
    exchange_thread.join()
